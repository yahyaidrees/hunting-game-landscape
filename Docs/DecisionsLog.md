# Decisions log

Every owner decision from section 21 of the game plan, with the date it was made. Until the owner answers, the agent uses the default. Status is **Default** (not yet answered) or **Decided**.

## Decided

| Date | # | Decision | Answer |
|---|---|---|---|
| 2026-11-19 | — | Old project render pipeline | Built-in. Old materials are converted to URP during the quality gate |
| 2026-11-19 | — | How old assets come in | Through the staging folder `Assets/_Incoming/OldGame/`. No old scripts are copied |
| 2026-11-19 | — | Where Phase 1 starts | Hunt-first graybox in the Meadow region. Final art comes after the hunt is fun |
| 2026-11-19 | — | Asset delivery | Play Asset Delivery through Addressables for Android: base about 150 MB, Region 2 fast-follow, later content on-demand. Plan section 17 updated |
| 2026-11-19 | 19★ | Meshy plan | Paid plan: private licence, no credit needed. Check the live terms on the first generation day and note the plan name in the licence log rows |
| 2026-11-19 | — | Staging in Git | `Assets/_Incoming/` and `SourceAssets/OldGame/` stay local only (6.6 GB of old assets); only gate-passed assets are committed |
| 2026-11-20 | — | Old environments (Alpine, Savanna, Snow) | Accepted as the starting base after the URP port, though they don't match the Built-in look. They'll be expanded (farm elements, more ground as the player walks) or replaced later if needed. Likely cause of the look difference: this project is Linear colour space, while the old art was baked for the old project's (probably Gamma) colour space. Regrade the materials when the look is worked on |
| 2026-11-19 | — | Unity version | 6000.3.26f1 (Unity 6.3 LTS), URP 17.3. Stay on this LTS line |

## Open (defaults in use)

| # | Decision | Default in use | Status |
|---|---|---|---|
| 1★ | Current D1/D7/D30, session length, DAU, installs per day, top 5 countries | "Today" column in section 2 stays empty | Default |
| 2 | Current ad and IAP revenue per daily user; ad networks and mediation | Section 11 example stays illustrative | Default |
| 3★ | Owner hours per week for review and phone testing | About 10 hours a week | Default |
| 4 | iOS later? | Android only; nothing blocks iOS | Default |
| 5 | Monthly budget for paid tools and assets | Free sources plus one paid Meshy plan | Default |
| 6★ | Old project folder; signing key, app ID, Play Console test track access | Owner provides in Phase 0; no secrets in the repo | Default |
| 7★ | Which old animal models and animations the owner holds licences for | Checked in the inventory; unclear means not reused | Default |
| 8 | Existing purchases that must keep working | Yes, all restored | Default |
| 9 | Existing Firebase project and Google Play Games set-up | Both set up new | Default |
| 10 | Keep current name or new title | Same name, new subtitle | Default |
| 11 | Blood level and age rating | Light blood with an off switch, teen rating | Default |
| 12 | Fixed farmer or choice of look; third person on farm | Choice of two looks; first person in hunts | Default |
| 13 | Setting (fictional South Asian foothills) | Accepted as in section 4 | Default |
| 14 | Fully offline? | Hunts and farm offline; ads, store, cloud save and on-demand packs need a connection | Default |
| 15 | Launch languages | English, then top languages from #1 | Default |
| 16 | Mentor voice | Text with subtitles plus about 40 AI-voiced lines | Default |
| 17 | AI-generated music acceptable? | Yes, from section 14 prompts | Default |
| 18 | Which AI sound tool and image tool | Prompts stay general | Default |
| 20★ | Three test phones (low, mid, high) | Owner names three; Low budget set from the weakest | Default |
| 21 | Paid Asset Store packs as fallback | Yes, after asking each time | Default |
| 22 | Five outside playtesters for gate B | Friends or family who have not seen the game | Default |
| 23 | Who updates privacy policy and Data safety form | Agent drafts, owner reviews and submits | Default |
| 24 | Soft launch: open test on main listing or second listing | Decided from installs per day (#1) | Default |
