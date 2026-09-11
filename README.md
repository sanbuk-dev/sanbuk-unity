# Sanbuk Unity SDK

Official Unity package for [Sanbuk](https://sanbuk.com) — publisher ad serving in the CPA model.

**[راهنمای فارسی →](README.fa.md)**

> **Status: first preview — Android only.** Banner, in-scene ads, interstitial and rewarded all work. iOS lands when the native iOS SDK grows its renderer.

## Installing

**Window → Package Manager → + → Add package from git URL:**

```
https://github.com/sanbuk-dev/sanbuk-unity.git
```

Nothing to resolve and nothing to configure. The native SDK travels inside the package as a plain `.aar` under `Runtime/Plugins/Android/`, not as a Gradle dependency — a dependency would be fetched from Google Maven or Maven Central at build time, which is slow or fails outright on plenty of the networks our publishers build on. An SDK that breaks somebody's build is an SDK they remove.

`kotlin-stdlib.jar` sits beside it for the same reason: the native SDK is written in Kotlin, and a Unity project has no Kotlin of its own. Leave both files where they are.

## Using it

```csharp
void Start()
{
    Sanbuk.Init("YOUR-MEDIA-CODE");
}
```

**A banner over your game** — the native SDK draws it, counts the view against the viewability bar, and routes the click:

```csharp
SanbukBanner.Show("HOME-TOP", SanbukBannerPosition.Bottom);
SanbukBanner.SetVisible(false);   // during a cutscene
SanbukBanner.Hide();              // done with it
```

**An ad inside your scene** — the same ad as data, drawn with your own uGUI, your own font, your own frame:

```csharp
Sanbuk.LoadAd("LEVEL-END", ad =>
{
    if (ad == null) return;       // a normal answer, not a failure

    titleText.text = ad.Headline;
    ctaText.text   = ad.CallToAction;
    labelText.text = ad.DisclosureLabel;
    StartCoroutine(LoadImage(ad.ImageUrl));

    ad.RecordImpression();        // when it is really on screen
    button.onClick.AddListener(ad.Click);
});
```

In a game this is usually the only option, and it is why the server describes ads instead of sending markup. A game renders into one surface and has no Android layout to host a view, so an ad that belongs on a signboard inside your world has to be drawn by your game. Two obligations come with that: call `RecordImpression()` only when the ad is genuinely on screen, and put `DisclosureLabel` somewhere on it.

**Full screen:**

```csharp
SanbukFullscreen.Load("LEVEL-END", ad => _interstitial = ad);

// ...later, when your game decides...
_interstitial?.Show(
    onShown:  PauseGame,
    onClosed: ResumeGame);
```

**Rewarded** — grant the reward in `onReward` and nowhere else. `onClosed` fires whether or not the player earned anything, so granting there pays out for a skipped ad:

```csharp
SanbukFullscreen.LoadRewarded("EXTRA-LIFE", ad => _rewarded = ad);

_rewarded?.Show(
    onShown:   PauseGame,
    onClosed:  ResumeGame,
    onReward:  () => GrantExtraLife(),
    onRefused: reason => CarryOnWithout(reason));
```

`onRefused` is not a log line to ignore. The interruption rules — nothing during a cold start, nothing back to back, a ceiling per session, a close control only after a few seconds — live in the native SDK, so a show can be refused and your game has to carry on as though it never asked.

## In the editor

Every call answers as though no ad were available: `LoadAd` gives you null, `Show` refuses with `not_available`, banners do nothing. Pressing Play never depends on an ad existing, and the empty-slot path — the one your game has to handle anyway — is the one you exercise while building.

## What this package is

A thin binding, deliberately. Matching, pacing, frequency caps, trial periods and budgets live on the Sanbuk server; the viewability rule, the offline impression queue and the interruption rules live in the native SDK. This package asks, draws and reports.

A build shipped to a store is frozen for months, so a rule hardened into a shell would outlive several changes of mind — and would have to be written again for every other shell, where the copies would drift.

## Notes for shipping

- **IL2CPP:** `link.xml` ships in the package. The SDK's classes are reached by name, so without it the linker strips them and you get an error only in a release build.
- **Threading:** every callback reaches you on Unity's main thread. You can touch the scene from any of them.
- **Permissions:** the native SDK declares `INTERNET` and `ACCESS_NETWORK_STATE`, and nothing else. No location, no phone state, no advertising id.
- **Other ad SDKs:** nothing here shares a class, resource or permission name with Tapsel, AdiveryPersian or AdMob, so it can sit beside whatever you already ship.

## Checking the package

Neither check needs an activated Unity licence, so a fresh clone and a CI box can run both:

```bash
Tests/compile.sh          # compiles Runtime and the sample with Unity's own Roslyn
python3 Tests/verify_jni.py   # checks every Java name this C# pronounces against the .aar
```

The second one matters more than it looks. C# happily compiles any string you hand to
`AndroidJavaObject`, so a wrong class name or a renamed getter is not a build error — it is a
crash on a player's phone, on the one screen that shows an ad. It has already caught a real
one: `Sanbuk.Init` built a `SanbukConfig` with two arguments, but the generated constructor
takes three.

## Related

- [Sanbuk Android SDK](https://github.com/sanbuk-dev/sanbuk-android) — the native SDK underneath
- [Sanbuk Flutter SDK](https://github.com/sanbuk-dev/sanbuk-flutter)
- [Sanbuk iOS SDK](https://github.com/sanbuk-dev/sanbuk-ios)
