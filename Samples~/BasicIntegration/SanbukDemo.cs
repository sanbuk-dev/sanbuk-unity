using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// One script showing all three shapes an ad takes in a game. Drop it on an
/// empty GameObject, fill in your media and placement codes, and press Build.
/// In the editor every call answers "no ad", which is the path worth seeing
/// first anyway.
/// </summary>
public sealed class SanbukDemo : MonoBehaviour
{
    [Header("From your Sanbuk panel")]
    [SerializeField] private string mediaCode = "YOUR-MEDIA-CODE";
    [SerializeField] private string bannerPlacement = "HOME-TOP";
    [SerializeField] private string nativePlacement = "LEVEL-END";
    [SerializeField] private string rewardedPlacement = "EXTRA-LIFE";

    [Header("Your own UI for the in-scene ad")]
    [SerializeField] private GameObject adPanel;
    [SerializeField] private Text headline;
    [SerializeField] private Text callToAction;
    [SerializeField] private Text disclosure;
    [SerializeField] private RawImage image;
    [SerializeField] private Button adButton;

    private SanbukFullscreen _rewarded;

    private void Start()
    {
        Sanbuk.Init(mediaCode, debug: Debug.isDebugBuild);

        if (adPanel != null)
        {
            adPanel.SetActive(false);
        }
    }

    public void ShowBanner()
    {
        SanbukBanner.Show(bannerPlacement, SanbukBannerPosition.Bottom);
    }

    public void HideBanner()
    {
        SanbukBanner.Hide();
    }

    /// <summary>The data path: we hand you an ad, you draw it.</summary>
    public void LoadInSceneAd()
    {
        Sanbuk.LoadAd(nativePlacement, ad =>
        {
            if (ad == null)
            {
                // Nothing matched, or the slot is capped for today. Leave the
                // space to the game — never show an error to a player.
                return;
            }

            headline.text = ad.Headline;
            callToAction.text = ad.CallToAction;

            // Not decoration: a player is owed the knowledge that this part of
            // the screen was paid for.
            disclosure.text = ad.DisclosureLabel;

            adButton.onClick.RemoveAllListeners();
            adButton.onClick.AddListener(ad.Click);

            adPanel.SetActive(true);

            // Reported here rather than in the callback above, because the ad
            // is only now actually on screen. Reporting on load counts ads
            // nobody ever saw.
            ad.RecordImpression();

            if (!string.IsNullOrEmpty(ad.ImageUrl))
            {
                StartCoroutine(FetchImage(ad.ImageUrl));
            }
        });
    }

    public void LoadRewarded()
    {
        SanbukFullscreen.LoadRewarded(rewardedPlacement, ad => _rewarded = ad);
    }

    public void ShowRewarded()
    {
        if (_rewarded == null)
        {
            return;
        }

        _rewarded.Show(
            onShown: () => Time.timeScale = 0f,
            onClosed: () =>
            {
                Time.timeScale = 1f;

                // Spent once shown, so the next reward needs a fresh load.
                _rewarded = null;
            },
            onReward: () => Debug.Log("Grant the extra life here, and nowhere else."),
            onRefused: reason => Debug.Log($"No ad this time ({reason}) — carry on without it."));
    }

    private IEnumerator FetchImage(string url)
    {
        using var request = UnityEngine.Networking.UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
        {
            image.texture = UnityEngine.Networking.DownloadHandlerTexture.GetContent(request);
        }
    }
}
