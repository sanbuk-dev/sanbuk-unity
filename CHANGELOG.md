## 0.1.0

* First release. Android only.
* `Sanbuk.Init` and `Sanbuk.LoadAd` for ads drawn with your own uGUI.
* `SanbukBanner.Show/Hide/SetVisible` — positional, because a game has no
  Android layout to host a view.
* `SanbukFullscreen.Load` / `.LoadRewarded` / `.Show` with `onShown`,
  `onClosed`, `onReward` and `onRefused`.
* Carries `ir.sanbuk:sdk-android` 0.2.0 and `kotlin-stdlib` as plain files
  under `Runtime/Plugins/Android/`, so a build resolves nothing over the
  network.
* `link.xml` included, so IL2CPP release builds keep the classes the SDK
  reaches by name.
