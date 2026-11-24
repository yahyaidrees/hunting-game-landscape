# Old project inventory (read-only)

Filled in on 2026-11-24 from the old project at `C:\Projects\huntinggame`. Nothing in the old project was changed. **No secrets are recorded here:** no keystore passwords, API keys or SDK keys. Ad unit IDs and app IDs are public (they ship inside every APK), so they are listed.

## Identity
| Item | Value |
|---|---|
| Old project path | `C:\Projects\huntinggame` (own git repo; last commit "final v2.0 (upgrade unity 6.0.58f2)") |
| Unity version | 6000.0.58f2 |
| Render pipeline | Built-in (no render pipeline asset on any quality level) |
| Product name / company | Animal Hunting Games / Metaphase Games |
| Application ID (package name) | `com.MetaphaseGames.PredatorHunting` |
| Current version code / version name | 99 / 2.0 |
| Android API levels | minimum 29 (Android 10), target 36 |
| Scripting backend / CPUs | IL2CPP / ARMv7 + ARM64 |
| Signing key | Custom keystore, held by the owner (path and passwords not recorded) |

**For the relaunch:** the new project must use the same application ID, the same signing key (or Play App Signing upload key), and a version code **above 99**.

## Store and SDKs
| Item | Value |
|---|---|
| In-app product IDs | `removeads` and `unlockall`, both non-consumable, Google Play (Unity IAP catalogue). **No game script or scene sells them today:** the in-app pop-up call is commented out. Check in Play Console whether they are active and whether anyone owns them. |
| Entitlement flags in the save | `ShowAds` (remove ads) and `BuyEverything` (unlock all) in PlayerPrefs |
| Ad network and mediation | AppLovin MAX, with the Google (AdMob) adapter |
| MAX ad unit IDs (Android) | Interstitial `ddc407766f89e4bf` · Rewarded `299c94ab4459a918` · Banner `50fe8035779d48c1` · Adaptive banner `33bee494635b62ce` · MREC `8cc8eb7cda7f45a9` · App open `755ca34253c6b010` |
| AdMob app ID (Google adapter) | `ca-app-pub-1160606341904088~3810113229` |
| Analytics | GameAnalytics (main analytics, used in 16 game scripts); Firebase Analytics 12.6 (logs ad revenue) |
| Crash reporting | Firebase Crashlytics 12.6 |
| Remote config | Firebase Remote Config 12.6 (one script) |
| Other Google | Play In-App Review (rate-us prompt), Play Core / App Bundle tools |
| Installed but not used by game code | Clever Ads Solutions (CAS), RevenueCat, Unity Purchasing code paths, standalone Google Mobile Ads (one script references it) |
| Debug / tools | SRDebugger, In-game Debug Console, Build Report Tool, Mesh Simplifier, Super Combiner |
| Secret files present (not copied, not read) | `google-services.json`, AppLovin SDK key in `MaxSdk/Resources/AppLovinSettings.asset`, the keystore |

## Save data
| Item | Value |
|---|---|
| Where progress is saved | PlayerPrefs (Android SharedPreferences for the app ID), plus `.pso` files in `persistentDataPath` written by `ObjectBuilderScript` (BinaryFormatter wrapping JsonUtility JSON) |
| Main keys | `Coins`, `Gun`, `OtherGun`, `LevelSelected`, `ModeSelected`, `OpenLevels`, `levelno`; mode keys `Adventure`, `PreyRescue`, `PredatorAttack`, `PrecisionHunt`, `StampedeHunting`, `DinoEscape`, `DinoAttack`; unlock flags `<Mode>ModeIsLocked` and `AdsRequiredToUnlock<Mode>Mode`; `ShowAds`, `BuyEverything`; tutorial flags `ShowTutorial*`; `ShowRateUs`, `ShowPopUpOnce`, `SoundOn`, `MusicOn`, `LowEndDevice` |
| Readable for the veteran reward? | **Yes.** An update on the same application ID keeps PlayerPrefs. On first launch the new game can read `Coins`, `OpenLevels` and the mode keys to grant the veteran reward, and must honour `ShowAds = 0` / `BuyEverything = 1` for players who paid. Old keys should be read once and then left alone. |

## Assets: keep or replace
Old scripts are never copied. Materials are Built-in and need the URP pipeline (as done for the wolf).

| Asset | Type | Path in old project | Licence known? | Size | Decision | Notes |
|---|---|---|---|---|---|---|
| Truebones "Z-OO" animals | Animals, animated | `Truebone_Z-OO/` | To confirm (Truebones) | 805 MB, 283 models | **Candidate for Phase 3** | Lion, leopard, jaguar, tiger, bear, brown bear, polar bear, fox, coyote, deer, stag, moose, gazelle, goat, sheep, camel, buffalo, ostrich, reindeer. Covers several plan predators (lion, leopard, bear, fox; coyote as a jackal stand-in) and livestock (goat, sheep). Put one through the wolf pipeline and judge it through the scope before using more. |
| Wolf | Animal | not in the old project | Asset Store (owner's library) | | **Done** | The old game had no wolf. The "Wolf Realistic" pack in staging came from the owner's Asset Store library; publisher still to confirm. |
| Animal pack deluxe | Small animals | `Animal pack deluxe/` | To confirm | 51 MB | Candidate for Phase 2 | Chicken and wild rabbit: farm livestock and small prey. |
| Birds | Birds, animated | `Prey Birds/`, `Predator Birds/`, `ANIMALS FULL PACK/Birds Pack` | To confirm | 250 MB | Fix, a few | Crows, eagle, owl and small birds for life in the regions and alarm calls. Not hunted. |
| Cheetah | Animal | `AfricanAnimalPack-2/` | To confirm | 30 MB | Leave | Not in the plan's animal list. |
| Dinosaurs | Animals | `Dino Assets/` (incl. `PrimalCarnage dinos`), `Jurassic Pack/` | **Doubtful** | 230 MB | **Do not carry over** | Off theme for a realistic hunting game. The folder name suggests models from the game *Primal Carnage*, which would be a licence problem. |
| First-person hunting rifle | Weapon (FP) | `YahyaFpsController/HQ FPS Weapons/.../SniperRifles/HuntingRifle/FP_HuntingRifle.fbx` | To confirm (HQ FPS Weapons) | | **Candidate for Phase 1** | Can replace the grey placeholder rifle. The same pack has a double-barrel and an R870 shotgun (later weapons). |
| Scoped rifles | Weapons | `YahyaFpsController/_GameAssets/3D/_Weapons/` (M24, M14 DMR, Mosin, CheyTac, Steyr Scout), `SniperAssets/`, `IndieGameModels/WWII Weapon Mobile/` | To confirm | | Candidate for Phase 2 | Store weapons. Military look may not fit; choose a few hunting-style rifles. |
| Meadow environment | Terrain pieces | `Models/Environments/MeadowEnvironment/` (hills) | To confirm | | Check for the Meadow final look | Compare with the URP-ported environments already in the new project. |
| Environment packs | Environment | `EnvironmentPacks/` (Autumn, FarmMesh), `Winter Ground Pack/`, `Textures/`, `Easy Mobile trees pack/`, `Mep 1/` (grass), `klen/`, `Skyboxes/`, `SnowTextures/` | To confirm | ~2.9 GB | Mostly covered | The three environments were already exported and ported to URP. Farm meshes (`FarmMesh`, `CowBarn.fbx`, `Farm house1.fbx`) are candidates for the Phase 2 farm. |
| Body splatter parts | Gore | `Body_splatter_parts/` | | 14 MB | **Do not carry over** | The plan: light blood, never gore. |
| Old UI | Sprites | `Gui/`, `pack sprite/` | | 85 MB | Replace | New UI style guide. |
| Infrared effects | Shader | `Infrared Effects/` | To confirm | | Leave for now | A thermal scope could be a later gadget. |
| Sounds | Audio | `Audio/` | To confirm | 10 MB | **Done** | Already staged; usable ones are in the new project (see the licence log). |
