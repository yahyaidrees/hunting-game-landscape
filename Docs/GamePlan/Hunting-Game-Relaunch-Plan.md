# Hunting Game Relaunch Plan: Farmer of the Jungle

Game plan · Android, Unity · Nov 12, 2026

This is the complete plan for rebuilding the game: design, monetization, UI, graphics, audio, production pipeline, technical set-up and roadmap. Section 1 explains how to use it.

1. How to use this plan
2. Vision and goals
3. Diagnosis and rebuild decision
4. Core fantasy and world
5. Core gameplay loop
6. Real hunting mechanics
7. Game modes and missions
8. Farm and livestock
9. Weapons, gear and progression
10. Retention systems
11. Monetization: 50% IAP, 50% ads
12. UI and screen design
13. Visual quality plan
14. Audio plan and sound prompts
15. Production pipeline and tools
16. Content plan
17. Technical plan
18. Store listing and relaunch marketing
19. Roadmap and phases
20. KPIs, testing and risks
21. Owner decisions and defaults
22. Sources

## 1. How to use this plan

This document is the single reference for rebuilding the game. It has two readers: the **owner**, who directs the work, and the **coding agent**, who builds it.

### Roles

| Role | Does |
|---|---|
| Owner | Makes the decisions in section 21. Logs into the web tools. Generates sounds from the prompts in section 14. Plays every build on real phones. Approves each gate in section 19. |
| Agent | Builds the game in Unity and Blender. Drives the web tools through the browser. Keeps the licence log. Checks work against the budgets. Reports what was done and what was not tested. |

In the game design sections, "you" means the player.

### Rules for the agent

1. **Follow the roadmap order** (section 19). Start with the phase 0 task list. Do not start a phase until the owner has passed the gate before it.
2. **Use the defaults** in section 21 until the owner decides otherwise. Ask the starred decisions first. Record every answer in the decisions log.
3. **Treat numbers as starting values.** Budgets, timers, prices and rewards in this plan go into data assets and remote config, never into code, so they can be tuned.
4. **No outside asset without a licence log entry** (section 15).
5. **The old game project is read-only.** Copy from it; never change it.
6. **Do not quietly change the design.** If something cannot be done as written (a tool fails, a budget cannot be met, a store rule has changed), stop and give the owner the options.
7. **Report honestly.** Say what was tested on a phone and what was only checked in the editor. Touch feel and sound quality are judged by the owner.
8. **Keep secrets out.** Passwords, keystore files and API keys never go into the repository or into documents.

### Project set-up in one line

The game is built as a **new Unity project** and released as an update to the **existing Play Store listing**. The old project is only a read-only source of reusable assets and settings. Section 3 gives the details.

## 2. Vision and goals

The relaunch turns a short, one-kill-per-level shooter into a farm-and-hunt game that players open every day for months: the player is a farmer living in the jungle who protects livestock by tracking and hunting predators the way a real hunter would.

**One-line pitch:** *Read the tracks, find the predator, protect the farm.*

The plan rests on five decisions:

1. **Theme first.** Every system (missions, upgrades, store, events, sound, UI) serves one story: a farm under pressure from the jungle.
2. **Hunting is a skill, not a button.** Tracking, wind, sound and patience are the gameplay, so a 5 to 10 minute hunt feels like a real hunt.
3. **A farm that needs you tomorrow.** Livestock, upgrades and timers give players a reason to come back every day.
4. **Hybrid revenue by design.** Rewarded ads and in-app purchases are both built into the loop from the start.
5. **One look, one sound, one pipeline.** Assets come from many sources (Meshy, Sketchfab, the current game, image and sound generators), so the plan fixes a style guide and quality gates before content is produced.

### Targets

Public 2026 benchmarks: median mobile games sit near 22% D1 and under 4% D7, a good profile is about 35/15/5 (D1/D7/D30), and the top quartile reaches about 40/20/10. Those figures are approximate (see Sources). The table has a **minimum** and a target. The minimum is the line for moving from soft launch to full launch. The target is what tuning should then reach.

| Metric | Today | Minimum to leave soft launch | Target, 3 months after relaunch | Target, 12 months |
|---|---|---|---|---|
| D1 retention | fill from Play Console | 30% | 35% | 40% |
| D7 retention | very low (fill in) | 10% | 15% | 20% |
| D30 retention | fill in | 3% | 5% | 10% |
| Average playtime per day | fill in | 12 minutes | 20 minutes | 30 minutes |
| Sessions per day | fill in | 2.5 | 3 | 4 |
| Revenue split (IAP / ads) | mostly interstitial ads | IAP at least 25% | 40% / 60% | 50% / 50% |

The minimum values are a starting proposal and the owner can change them. The split starts ad-heavy on purpose: ads earn from day one, while IAP revenue grows as players build a farm and invest in it.

## 3. Diagnosis and rebuild decision

The current game is weak at D7 because a player finishes everything worth doing in the first two sessions and has no reason to return. Retention work should fix the cause, not just add a daily reward.

| What the game does now | Why it hurts retention or revenue | What changes |
|---|---|---|
| Tutorial walks through menus, then gun controls | The first minute is reading, not hunting | The first minute is a hunt. Menus are taught later, one at a time, when each is first needed |
| Level ends after one kill | Sessions are 1 to 2 minutes, no tension, no story | Hunts have several stages and last 5 to 10 minutes |
| Cut scene before every level | Friction repeats on every level | Cut scene only for key moments, always skippable after the first view |
| Interstitial after every level | Ads hit before the player is hooked; players uninstall | No forced ads in the first session; rewarded ads first, interstitials capped |
| Fixed position, gun only | No skill growth, no exploration, no variety | Walking, tracking, stalking, multiple tools |
| Small levels with little detail | The game looks and feels small | A few large regions built to a written detail standard (section 4) |
| No farm or long-term goal | Nothing to come back for | Farm, livestock, upgrades, daily and weekly goals |
| Tiny or missing store | No spending path for engaged players | Full store with starter offers, gear, passes and farm items |

### Project set-up: a new project on the same store listing

**Decision: build the game as a new Unity project and release it as an update to the existing Play Store listing.** Almost everything is being rebuilt, so starting clean costs little and avoids carrying old code, old scenes and old render settings into the new game.

| Item | Rule |
|---|---|
| New project | Current Unity 6 LTS, Universal Render Pipeline from the first day, its own Git repository, the folder structure in section 17. |
| Store identity | Same application ID (package name), same signing key, and a higher version code than the live build. This is what keeps the ratings, the installs and the listing. |
| Purchases | Same in-app product IDs for anything players already bought (for example remove-ads), so existing purchases are restored. |
| Old project | Read-only. The agent opens it to list reusable assets and to read the application ID, product IDs, ad unit IDs, SDK list and save format. Nothing in it is changed. |
| Reuse | Optional, not required. An old asset is copied across only if it passes the same quality gate as a new one (section 15). Animated animals are the most valuable thing to look for. The roar zoom-in intro is kept as an idea and rebuilt. |
| Old saves | Because the application ID is the same, the old save data is still on the player's phone after the update. If its format can be read, the new game reads it once and turns it into a veteran reward. |

What this costs: the ad, billing and analytics SDKs are integrated again from scratch, which is needed anyway to meet current Play requirements, and any reused model needs its materials converted to the new render pipeline.

### How the new game reaches players

A total remake pushed straight onto a live listing is risky: if it is not ready, the rating drops and is slow to recover. Use this order:

1. **Closed test track** for controls and tracking clarity.
2. **Soft launch.** Either an open test track on the main listing, or a temporary second listing in one or two countries. A second listing protects the main rating completely but its results do not carry over. Choose after seeing how many installs per day the main listing gets.
3. **Staged rollout** on the main listing (10%, 50%, 100%) once the minimum numbers in section 2 are reached.

## 4. Core fantasy and world

The player inherits a small farm at the edge of a jungle valley. Livestock is the family's living, and predators are taking it. The player is not a trophy hunter: they hunt only the animals that threaten the farm, and learn to do it the hard way, by reading the land.

- **The player:** a farmer with a name, a customizable look, and a farm that visibly grows from a few goats and a wooden pen to a thriving ranch.
- **The mentor:** an old ranger who replaces today's menu tutorial. He teaches tracking, wind and shooting through the first real hunts, so the tutorial is the first story mission and not a separate lesson.
- **The companion:** a hunting dog that can sniff out trails and warn of danger. It arrives as a puppy on day 1 (a strong reason to come back after the first session), grows over the first two weeks, and is upgradeable.
- **The legend predators:** each region has one named, scarred, hard-to-find predator (for example an old lion that has avoided hunters for years). These are multi-day story hunts and the main long-term goal of each region.

### Setting

The predator list includes lion, bear, leopard and wolf. These do not share one habitat in most of the world, and a game that sells real hunting detail should not feel like a zoo. A simple fix: set the valley in fictional forest foothills loosely based on South Asia, where leopard, wolf, bear, striped hyena, jackal, wild boar and even lion all have real ranges. The player never needs to be told the country. The benefit is that vegetation, light, village props and music all point the same way, which is what makes a world feel detailed. It also leaves the tiger free as a future headline predator. This is the default setting unless the owner chooses another (decision 13).

### World structure

| Region | Unlocks | Predators (examples) | Feel |
|---|---|---|---|
| Farm edge and meadow | Start | Fox, jackal, wild dog | Open, easy sight lines, learn the basics |
| Pine forest | Farm level 3 | Wolf packs, lynx | Cover, wind matters, packs flank you |
| Dense jungle | Farm level 6 | Leopard, wild boar | Thick cover, night hunts, close range |
| Rocky highlands | Farm level 9 | Bear, mountain cat | Long shots, weather, charging animals |
| Dry grassland border | Farm level 12 | Lion, hyena | Open plains, groups, legend hunt finale |

The first three regions ship at relaunch. The final animal list should follow which models and animations already exist, since animation cost is the biggest content limit.

### What one region contains

This is the written standard for "detail like a big game". Every region is built to it, and the hunt generator (section 16) relies on it.

| Element | Standard |
|---|---|
| Size | About 400 by 400 metres of hand-detailed playable ground, with low-detail terrain beyond it as backdrop. A trail of 150 to 300 metres fits inside with room for detours. |
| Landmarks | 6 to 8 named places the player learns: waterhole, stream crossing, rocky outcrop, fallen giant tree, old watch platform, cave or den, abandoned cart, salt lick. They are navigation points, clue sites and shooting positions. |
| Animal layout | 3 den sites per predator species and 10 to 12 authored trail routes between landmarks. The generator picks from these so no two hunts start the same way. |
| Ground types | Mud, grass, dry leaves, rock, shallow water. Each has its own footprint look, footstep sound and noise value. Mud holds perfect prints; rock holds none, so the player must search around to pick the trail up again. |
| Height | At least one high point for long shots and scanning, and one low, dense area for close tracking. |
| Time and weather | 4 lighting presets (dawn, noon, dusk, night) and 3 weather states (clear, rain, fog). |
| Ambient life | Birds that burst out of cover when a predator moves, non-target animals (deer, rabbits, monkeys), insects, butterflies, fireflies at night. Ambient life is both atmosphere and a tracking clue. |
| Human touches | Paths, fences, a shrine or marker stones, old traps, a neighbour's field. These tie the wild region back to the farm story. |

### Tone and rating

Keep the tone protective, not cruel: the reward is a safe farm, not gore. Keep blood light with an option to turn it off, never show livestock death graphically, and answer the store content rating questionnaire honestly based on what the final game shows.

## 5. Core gameplay loop

Three loops run at once: a 5 to 10 minute hunt, a daily farm routine, and a weeks-long climb through regions and legend hunts. Each one feeds the next, so even a short session moves a longer goal forward.

| Loop | Step 1 | Step 2 | Step 3 | Step 4 |
|---|---|---|---|---|
| Session loop (every hunt) | Alert: raid on the farm | Track: clues and trail | Approach: wind and shot | Reward: coins and stars |
| Daily loop (every day) | Collect: farm income | Hunt: daily missions | Check: trail cameras | Spend: upgrade the farm |
| Long-term loop (weeks and months) | Level up: farm and hunter | Unlock: new regions | Chase: legend predators | Complete: guide and pass |

How the loops connect: the Reward step of the session loop (coins and materials) feeds the Spend step of the daily loop. The Spend step (upgrades raise the farm level) feeds the Level up step of the long-term loop.

Three nested loops. Hunt rewards pay for farm upgrades, upgrades raise the farm level, and farm level opens new regions and legend hunts.

- **Session loop:** a full hunt fits a short break, and tracking is the part players should remember.
- **Daily loop:** farm income, trail cameras and the daily hunt are the reasons to open the game every day.
- **Long-term loop:** regions, legend hunts and the field guide are the reasons to stay for a month.

## 6. Real hunting mechanics

This is the feature that makes the game different from other animal shooters. A hunt is a chain of decisions: what attacked, where did it go, how fresh is the trail, which way is the wind, and how do I get close without being noticed. Each step must be clear and quick to read on a phone, never a chore.

### The hunt chain

1. **Alert:** livestock is attacked, a pen is broken, or the dog barks. A hunt mission starts from the farm.
2. **Investigate the scene:** inspect the ground around the pen. Bite spacing, claw marks and drag marks show which predator it was and which way it went.
3. **Find and follow the trail:** footprints and other signs lead you in stages. Each clue you find draws a new section of the trail on your hunter's map.
4. **Read the freshness:** a track from this morning looks different from one from yesterday. You decide whether to follow it or search for a better one.
5. **Approach:** use wind, cover and quiet movement to close the distance.
6. **Take the shot:** a clean shot ends the hunt quickly. A poor shot sends the animal running with a blood trail you must follow.
7. **Return:** deliver the result, repair the pen, collect rewards, and see the farm change.

### Tracking signs

| Sign | What it tells you | How the game shows it |
|---|---|---|
| Footprints | Species, size, direction, walking or running (stride length) | Tap a print to inspect; compare against the species card in your field guide |
| Track freshness | How long ago the animal passed | Sharp edges and moisture for fresh, crumbled or leaf-covered for old; a clear fresh, medium or old label for casual players |
| Scat | Recent diet, freshness, territory | Inspect to confirm species and time window |
| Fur and broken twigs | Direction of travel and size | Highlighted when you are close; the dog can also find them |
| Claw marks on trees | Territory marking, leopard or bear | Opens a short clue card with a hint for the next step |
| Drag marks | A predator carried its kill this way | A wide scuffed line that leads toward a feeding spot |
| Kill site | Predators return to feed, often at dusk | Unlocks a stakeout: wait in cover, or place a trail camera |
| Alarm calls | Birds, monkeys and deer call out when a predator moves nearby | A sound with a direction, plus a small pulse on the compass for players with sound off |
| Blood trail | A wounded animal's path | Appears after a poor shot; follow it before the animal recovers |

> **Accuracy is a feature.** Dog-family tracks (wolf, jackal, fox, hyena) show claw tips; cat-family tracks (lion, leopard, lynx) do not, because cats keep their claws pulled in. A bear print shows five toes and a wide pad. Small true facts like these go in the field guide, make the player feel smart, and cost almost nothing to build. All track art must be checked against real references before it ships.

To keep tracking fun and not slow, every trail has three to five clues, each found within a short walk, and a **Hunter Sense** ability briefly highlights the nearest clue. Hunter Sense has a cooldown and can be refilled by watching a rewarded ad or by using a consumable.

### Tracking mastery curve

Tracking must get deeper as the player improves, or it is solved in the first week. New ideas arrive one at a time.

| Stage | When | What is new | Help given |
|---|---|---|---|
| 1. Follow | Hunts 1 to 5 | One clear trail on soft ground. The player learns to inspect, read direction and use the map. | Clues glow; the mentor names each sign |
| 2. Judge | Hunts 6 to 15 | Freshness matters: two trails, one old. Wind and noise are introduced. | Fresh or old labels; wind arrow always visible |
| 3. Search | Hunts 16 to 30 | Trails cross with non-target animals. Rock and water break the trail and the player must circle to find it again. Rain washes old signs away. | Glow only with Hunter Sense; the dog helps on request |
| 4. Outthink | Legend hunts and events | The predator doubles back, uses streams to hide its trail, and moves its den between sessions. Clues gathered over several days narrow down where it will be. | No glow; evidence board in the hunter's lodge |

A player can lower the assist level at any time for a **tracker bonus** (more coins and field guide progress), so skilled players choose the harder game instead of having it forced on them.

### Senses and environment

- **Wind:** a visible wind indicator (dust or grass blowing, plus a compass arrow). Approach from downwind or the animal smells you and runs. Different animals have different noses.
- **Sound:** a noise meter. Dry leaves and branches are loud, soft ground and mud are quiet, crouching is quietest but slow.
- **Sight and cover:** bushes and tall grass hide you. Camouflage gear improves this. Movement, not just standing in view, is what spooks animals.
- **Weather and time:** rain hides sound but washes tracks away; fog limits both sides; night makes animals bolder and needs a light or night scope.

### Animal behavior

| Animal | Behavior to build |
|---|---|
| Fox, jackal | Quick, nervous raiders of chickens. Run at the first noise and return later. The teaching animals. |
| Wolf | Pack of 3 to 5, flanks and circles, the alpha can be identified |
| Wild boar | Destroys crops and fences, travels in a group, charges in a straight line when cornered |
| Leopard | Stalks at dusk and night, drags its kill into a tree and returns to it |
| Lion | Ambush attack, often with a partner, loud and stubborn when hunted |
| Bear | Slow and cautious, will charge if wounded or cornered |
| Hyena | Scavenges, arrives after a kill, can be lured |

Animal states: roam, hunt livestock, feed, rest, alert, flee, charge. A predator that is alerted but not sure stays still and listens, which creates real tension.

### Tools

- **Binoculars:** scan and tag animals from far away.
- **Trail camera:** place it near a kill site and check it later; it reveals what comes at night and gives a real reason to return after a few hours.
- **Bait and scent lure:** pull an animal to a spot you can shoot from.
- **Traps:** catch smaller raiders so livestock stays safe while you chase a bigger one.
- **Call:** attract or distract certain animals, with a limited number of uses.
- **Wind checker:** a puff of powder that shows the exact wind for a few seconds. A cheap consumable and a real hunting tool.

### Shooting

Keep the current gun and zoom feel, then add the real parts: breath hold to steady the aim, sway that depends on stamina, bullet drop at long range, and wind drift for upgraded weapons. Shot placement matters: heart and lung shots kill quickly and give a bonus, while body or leg shots leave a blood trail. A slow-motion kill camera plays on perfect shots as a reward moment.

### Fair play and ethics

Only marked predators can be hunted. Identify your target first (species, size, and for some animals, whether it is leading a pack). Shooting a protected or non-target animal costs reputation and coins. This teaches careful hunting and keeps the game on the right side of store policies.

### Controls

Landscape layout. Left thumb moves, right thumb looks, and buttons handle shoot, scope, crouch, hold breath, binoculars and a tool wheel. An **assist level** setting (low, medium, high) adjusts aim help and track highlighting. The full HUD layout is in section 12.

## 7. Game modes and missions

The modes of the current game are not thrown away. They are rebuilt as short, fast modes inside the bigger game, which gives players both a quick 2 minute session and a long 10 minute hunt.

| Mode | What it is | Session | Built from |
|---|---|---|---|
| Story hunts | Main campaign: investigate, track, approach, shoot. Region by region | 5 to 10 min | New |
| Rescue hunt | A predator is attacking livestock right now; stop it before it kills. The roar zoom-in intro stays | 1 to 3 min | The current game's first mode |
| Watch post | Defend the pen from a fixed tower position, in waves or through the night | 2 to 4 min | The current game's fixed-position gun |
| Free hunt | Walk the region, find and hunt animals at your own pace | 5 to 15 min | The current game's moving mode |
| Legend hunt | Multi-day story hunt for a named predator, with clues found across several sessions | 3 to 5 days | New |
| Daily hunt | One generated hunt per day with a special reward and a streak bonus | 3 to 6 min | New |
| Weekend events | Limited-time predator invasions and leaderboards | Varies | New |
| Shooting range | Test weapons and scopes with no pressure | Any | New, small |

### Mission types

Mix these inside story hunts so no two feel the same:

- **Track and hunt:** the standard chain from scene to shot.
- **Rescue:** time pressure, a live animal in danger.
- **Defense:** hold a position against raiders.
- **Stealth:** reach the target without alerting any other animal.
- **Stakeout:** find the kill site, choose a hiding place downwind, and wait for the predator to return.
- **Trap and survey:** place traps and trail cameras, return later to collect results.
- **Long shot:** distance and wind challenge from a high point.
- **Herd escort:** guide livestock to safety across a risky path.
- **Night hunt:** limited visibility, a light and night scope required.

Every mission has three optional goals (for example: clean shot, no livestock lost, under a time limit). Optional goals give stars, and stars unlock chests. This is the replay hook that makes one mission worth playing twice.

## 8. Farm and livestock

The farm is the home screen and the heart of retention. It is a real 3D scene, not a menu: the camera glides over the farm, animals walk in their pens, and the player taps buildings to open them. Players should see their farm grow, feel responsible for the animals, and have something to collect or build every time they return.

### How it works

- **Livestock** (goats, sheep, cows, chickens, horses) live in pens and slowly earn coins while they are healthy. Earnings are collected when you open the game; a rewarded ad doubles a collection.
- **Predators cut income.** While a predator is active, affected livestock earn less. Livestock is never permanently lost, so one bad day cannot make the player quit. The pressure is felt as lost income and a worried farm, not as punishment.
- **Predator pressure rises over time.** If you do not hunt for a day, a new raid appears. This gives a natural reason to return without a harsh penalty.
- **Upgrades take time.** Building and upgrade timers run from 1 minute early on to many hours later. Players can wait, spend gems, or watch an ad to cut time.
- **The farm shows your history.** Each building has three visible upgrade levels, and trophies from legend hunts appear on the lodge wall.

### Buildings

| Building | What it does |
|---|---|
| Pens and fences | Protect livestock; higher levels lower raid damage and raise income |
| Watchtower | Unlocks the Watch post mode and extends sight range |
| Workshop | Craft ammo types, traps and weapon parts |
| Hunter's lodge | Field guide, trophy wall, region maps, legend evidence board, mentor missions |
| Kennel | Raise and train the hunting dog |
| Camera station | Manage trail cameras and review their photos |
| Vet hut | Heal injured livestock faster |
| Trading post | Sell goods, daily deals and traveling trader offers |

### Collections

The field guide records every animal, track, and gear item you discover. Each page has levels (seen, tracked, hunted, perfect shot), and completing a region's guide gives a permanent bonus. Collections are a core 30 day retention tool because they give long, visible goals.

## 9. Weapons, gear and progression

Players should feel stronger every session, and every upgrade should change how a hunt plays, not only a number.

### Weapons

| Weapon | Strength | Weakness | Fits |
|---|---|---|---|
| Hunting rifle (start) | Balanced range and damage | Loud | Everything early on |
| Bolt-action rifle | Long range, high damage | Slow reload | Highlands, long shots |
| Shotgun | Close-range power | Short range | Dense jungle, charging animals |
| Bow and crossbow | Silent, no alarm to other animals | Low range, high skill | Stealth missions |
| Tranquilizer rifle | Non-lethal, can drive an animal off | Limited to some missions | Optional, softer-tone path |

Each weapon has stats (damage, range, stability, reload, noise) and slots for scope, barrel and stock. Weapons rise through tiers (common to legendary) using parts found in hunts and crafted in the workshop.

### Gear

Boots (less noise), clothing (camouflage and scent cover), binoculars, scopes, trail cameras, bait, traps and special ammo. Gear solves problems, so a player who struggles with a wary leopard can fix it with better camouflage instead of grinding a weapon.

### Player progression

- **Hunter level:** earned from hunts; unlocks gear and weapons.
- **Farm level:** earned from buildings and livestock; unlocks regions.
- **Skill tree:** three branches, each with a few points per level: *Tracking* (clues stay visible longer), *Stealth* (quieter, better camouflage), *Marksmanship* (steadier aim, bonus on clean shots), plus a small *Farming* branch for income.
- **Reputation:** each region remembers you, and neighbors offer better contracts as it grows.

### Currencies

| Currency | Earned by | Used for |
|---|---|---|
| Coins | Hunts, livestock, selling | Everyday upgrades and ammo |
| Gems | Rare drops, achievements, purchase | Premium items, time skips, cosmetics |
| Materials (wood, iron, leather) | Hunts and crafting | Buildings and weapon parts |
| Hunter marks | Events and the season pass | Event shop and exclusive gear |

Avoid a hard energy system that blocks play. For a hunting game, limits feel better as upgrade timers and daily event tickets than as a bar that stops the hunt.

### Economy rules and starting values

These are first-pass values so the prototype has something to tune. All of them live in remote config and will change after the soft launch.

| Rule | Starting value |
|---|---|
| Time between meaningful unlocks | Week 1: something new every session. Weeks 2 to 4: something new every day. After that: every 2 to 3 days, with events filling the gaps. |
| Story hunt reward | 100 coins base, plus 50 per star, plus 1 to 3 materials. A 3-star hunt pays 2.5 times a 0-star hunt, so skill is rewarded. |
| First upgrades | The first three farm upgrades cost less than one hunt each, so the first session always ends with visible growth. |
| Upgrade timers | 1 minute, 5 minutes, 30 minutes, 2 hours, 8 hours as a building rises. The first timer over 1 hour should appear at the end of day 1, so there is a finished upgrade waiting on day 2. |
| Farm income | A full offline collection equals about one story hunt. Storage fills in 8 hours, which rewards two visits a day without punishing one. |
| Free gems | Enough over 7 days of normal play to buy one small premium item, so players learn what gems do before being asked to buy them. |
| Free-player pace | A player who never pays can finish every story hunt. Paying buys speed, comfort and style, never access to the main story. |

## 10. Retention systems

Each retention milestone has a different cause, so each gets different tools. A weak D1 is usually an onboarding problem, a weak D7 is a habit problem, and a weak D30 is a depth and live-ops problem.

| Window | The player's question | What the game answers with |
|---|---|---|
| First session | Is this fun, and do I understand it? | A real hunt in the first 60 seconds, first kill with the mentor, the farm reveal, a first upgrade, and no interstitial ads |
| Day 1 | Is there more? | The puppy arrives, trail camera photos are waiting, an upgrade has finished, a daily reward, the first legend clue |
| Days 2 to 7 | Do I have a routine here? | Daily hunt and 3 daily missions, a 7-day login ladder ending in a weapon, the first region unlock, a starter offer expiring on day 3 |
| Days 8 to 30 | What am I working toward? | Legend hunts, field guide pages, farm upgrades, weekly events, season pass |
| Day 30 and later | Is there still something new? | New regions and animals, seasonal events, leaderboards, a hunting club |

### First session script

1. Open on the jungle, the predator's roar, the mentor's voice. Skip the menu tour.
2. Tutorial hunt: inspect one clue, follow one track, one shot. Controls are taught during action.
3. Show the saved livestock and the farm. Give a free upgrade right away.
4. Place the first trail camera and say it will have photos in a few hours. The mentor promises to bring a puppy tomorrow. These are the first two reasons to return.
5. Ask for notification permission only now, after the player has had a win.

Target length: 10 to 15 minutes, with the first kill inside the first 3 minutes.

### The first seven days

D7 is won or lost here, so each day has one headline unlock. A player who leaves on any evening should already know what is waiting tomorrow.

| Day | Headline | Also happens | Reason to return tomorrow |
|---|---|---|---|
| 0 | First hunt and the farm reveal | Free first upgrade, first trail camera placed, starter rifle | The puppy, the camera photos |
| 1 | The hunting dog arrives | Camera photos reveal a wolf, hunts 3 to 5, daily missions open, starter pack offer appears | The workshop finishes building overnight |
| 2 | Workshop and first craft | Daily hunt mode opens, hunter pass becomes visible, first scope | The mentor has found a way into the pine forest |
| 3 | Region 2: the pine forest | First wolf pack, wind is taught, starter pack expires tonight | The first legend clue was found at the kill site |
| 4 | Watchtower and Watch post mode | Bow unlocks, first stealth mission | The evidence board opens in the lodge |
| 5 | Legend hunt, part 1 | First stakeout, the dog learns to follow a scent | The legend moved; a camera is watching |
| 6 | First night hunt | Weekend event opens, leaderboard | The login ladder pays its big reward tomorrow |
| 7 | Bolt-action rifle from the login ladder | Legend hunt finale becomes possible, first trophy on the wall | Region 3 is in sight; the season pass has three weeks left |

### Daily and weekly rhythm

- **Daily:** 3 missions, one daily hunt, a free daily chest, livestock income to collect, and a trail camera check.
- **Weekly:** a weekly challenge with a bigger reward, a weekend event and a leaderboard reset.
- **Streaks:** a login streak that is forgiving (miss a day and the streak pauses, it does not reset to zero), because harsh resets cause quitting.

### Notifications

Maximum two per day, opt-in, and always tied to something real: "Your trail camera has new photos," "Wolves were seen near the pen," "Your workshop upgrade is done." No generic "come back" messages.

### Social and safety nets

- **Cloud save with Google sign-in** from day one, so players do not lose their farm when they change phones.
- **Leaderboards and achievements** through Google Play Games.
- **Share a clip** of a perfect shot kill camera, which is cheap organic marketing.
- **Hunting clubs** (small groups with shared goals) come in a later phase once the base game retains well.

## 11. Monetization: 50% in-app purchases, 50% ads

A 50/50 split is reachable, but it does not happen by accident. Public 2026 data shows IAP-first action and strategy games earn most of their revenue from purchases (around 80% in one tracker), while hybrid-casual games run closer to 45/55. Android also leans toward ad revenue. So the plan is to design the ad side to be strong, not weak, and the store to be deep. Figures are approximate, see Sources.

### The arithmetic of 50/50

Track one pair of numbers every week: **ad revenue per daily active user** and **IAP revenue per daily active user**. The split is 50/50 when they are equal.

| Side | Formula | Worked example (illustrative inputs only) |
|---|---|---|
| Ads per daily user | (rewarded views per user × rewarded eCPM + interstitial views per user × interstitial eCPM) ÷ 1000 | (5 × $8 + 2 × $5) ÷ 1000 = **$0.050** |
| IAP per daily user | share of daily users who buy × average purchase × the developer's share after the store fee | 1.2% × $5.00 × 0.85 = **$0.051** |

The inputs are placeholders to show how the sum works, not forecasts. eCPM can differ by ten times between countries, and the store fee depends on the Play Console programme the account is in. Replace every input with the game's real mediation and Play Console data in phase 0.

What the sum shows: the two halves are driven by different levers, so each can be steered without hurting the other.

- **If IAP is behind:** improve the starter pack and its timing, add offers at moments of need (out of bait before a legend hunt), deepen the hunter pass, and check payment methods in the game's top countries.
- **If ads are behind:** add rewarded placements where the player already wants something, raise opt-in with better rewards, and improve mediation. Do not answer by adding interstitials.
- **If both are low:** the problem is retention or playtime, not monetization. Go back to section 10.

### Rewarded ads (the main ad income)

Players choose these, they feel fair, and they are the best-paying and best-liked ad type. Every placement gives something the player wants.

| Placement | Reward | When it appears |
|---|---|---|
| Hunt result screen | Double coins and materials | After every successful hunt |
| Livestock income | Double the collection | On the farm when income is ready |
| Upgrade timer | Cut 30 to 60 minutes | When a timer is running |
| Hunter Sense empty | Refill sense for one hunt | When the ability is on cooldown |
| Retry after failure | One extra attempt with the trail kept | After a failed hunt |
| Daily chest | A second free chest | Once or twice per day |
| Trail camera | Reveal photos early | While a camera is waiting |
| Extra ammo or bait | One special item | Before a hunt starts |
| Trader's free offer | One free item from the trading post | Refreshes every few hours |

A target to test, not a promise: 4 to 6 rewarded ad views per daily active user per day once the player is past day 3. Tune against retention, because more ads that cut sessions lose money.

### Interstitial ads (used carefully)

- None in the first session or the first 10 minutes of play.
- Only at natural breaks (after a hunt result, never during a hunt or while tracking).
- A minimum gap between ads (start with 3 minutes) and a daily cap, tested through remote config.
- Never directly after a rewarded ad.
- Any purchase can switch them off for that player, which also counts as an IAP.

Banners stay out of gameplay. Use mediation with at least two or three ad networks so they compete for each impression; AppLovin and Google AdMob together carry about 65% of mobile game ad revenue in 2026 tracking.

### Store design

The store is the second pillar and today it is almost missing. Build it as one screen with tabs along the top: Offers, Gems, Coins, Gear, Farm, Pass, Style. It opens on Offers. The gem and coin counters at the top of every screen have a plus button that opens the store on the right tab.

| Product | What it is | Why it works |
|---|---|---|
| Starter pack | One-time offer of a strong weapon part, gems and coins, available for 48 hours after it first appears on day 1 | Lowest-price first purchase; converts a free player into a payer |
| Hunter pass | Monthly pass with a free track and a premium track | Gives a month-long goal; reliable recurring revenue |
| No-interstitial pack | One purchase that removes forced ads; rewarded ads stay optional | Players who dislike ads pay to remove them |
| Gem packs | Five tiers | The basic currency sale, with a clear best-value tier |
| Farm packs | Premium livestock, faster pens, builder speed-ups | Tied to the farm, so the spending has a visible result |
| Weapon and gear bundles | Limited-time bundles tied to events and regions | Creates urgency without blocking free players |
| Style items | Outfits, dog skins, weapon skins | Spending with no gameplay advantage, popular with long-term players |
| Hunter's chest | A growing chest of gems earned while playing, unlocked with a purchase | Shows value the player already earned |
| Region pack | Offered once when a new region unlocks: the gear that region needs | The right offer at the moment of need |
| Farmer's club (later) | Monthly subscription: daily gems, bonus income, fewer ads | Adds a recurring base once the game is stable |

Set prices in Google Play's regional price tiers, and check which payment methods the game's biggest countries actually use (for example carrier billing or gift cards) because card access can limit purchases.

### Rules that protect the game

- Never lock the core hunt behind payment. Free players must be able to finish every story hunt.
- No fake timers or misleading buttons. Ad close buttons must work.
- Show real chances for any random box, and prefer fixed-contents offers.
- Keep the interstitial frequency a remote-config value so it can be changed in a day, without a new build.

## 12. UI and screen design

Design, UI and game loop must be consistent. Consistency comes from deciding the screen structure and the style once, before any screen is built.

### Screen map

- Loading → Farm (home) → Settings, Credits
- Farm (home) has a bottom bar of five tabs: Buildings, Hunt board, Armory, Field guide, Store
- Hunt flow: Hunt board → Briefing and loadout → Hunt (HUD) → Result → back to Farm (home)

After the result screen the player always lands back on the farm, where the reward is spent.

Every screen is at most two taps from the farm. Events, the hunter pass and daily missions open as panels over the farm, not as separate places.

| Screen | Contents |
|---|---|
| Farm (home) | Live 3D farm. Top bar: player level, coins, gems, settings. Left side: daily missions, events, pass. Right side: raid alerts and the big Hunt button. Tap a building to open it. |
| Hunt board | Region map with pins: story hunt, daily hunt, rescue, watch post, free hunt, legend. Each pin shows the predator, the reward and the stars already earned. |
| Briefing and loadout | What happened, which predator is suspected, wind and weather, recommended gear, the three optional goals. One rewarded ad slot for an extra item. |
| Result | Shot replay still, stars, rewards counting up, field guide progress, double-reward ad button, then Back to farm. |
| Armory | Weapons, attachments, gear, the dog. Stat bars compare before and after an upgrade. |
| Field guide | A paper journal with a page per animal: sketch, track drawing, habits, the player's best shot. |
| Store | Tabs as in section 11. Each offer card shows contents, price and time left. |
| Settings | Graphics quality, controls, assist level, sound, language, account, privacy, **Credits**. |

### Hunt HUD

| Screen position | Element |
|---|---|
| Top left | Objective panel with clue progress (for example "clues found 2 of 4") |
| Top centre | Compass and wind arrow |
| Top right | Hunter's map, pause button |
| Left edge, middle | Hunter Sense button |
| Bottom left | Move stick |
| Right half of the screen | Drag anywhere to look |
| Bottom right | Fire button (largest), with scope, reload, crouch and hold-breath buttons around it |
| Right edge, upper | Tool wheel button |
| Bottom centre | Noise and visibility meters |

Landscape HUD. Thumbs stay in the lower corners, information stays along the top, and the centre of the screen is kept clear for the hunt.

- The HUD hides what is not in use. While tracking, the fire and scope buttons shrink; when a weapon is raised, tracking buttons fade.
- Button size and position are adjustable in settings, and there is a left-handed layout.
- Everything important has a visual signal as well as a sound, because many mobile players play muted.

### Style guide

The look is a hunter's field journal: paper, ink, wood and leather. It fits the theme, it is cheap to produce consistently with image generation, and it does not compete with the 3D world behind it.

| Element | Rule |
|---|---|
| Colours | Forest green **#2F4A36** (panels and headers), bark brown **#4A3728** (frames), parchment **#EFE4CC** (content background), lantern amber **#E9A23B** (the one main action button on each screen), alert red **#C8452F** (raids and warnings only), slate **#5F7C8A** (secondary information). |
| Rarity colours | Grey, green, blue, purple, gold. Used the same way on weapons, gear, chests and offers. |
| Type | One slab-serif display font for titles and numbers and one clean sans-serif for body text, both under an open licence that allows use in apps. Rendered with TextMesh Pro. No text smaller than about 14 points at a 1080p reference screen. |
| Panels and buttons | Parchment panels with a thin ink border and a wooden header strip. Buttons look like stitched leather tags. All built as 9-slice sprites so one image serves every size. |
| Icons | Hand-inked line with flat colour fill, one line weight, same three-quarter angle for all objects. Generated in batches from one prompt template (section 15) so they match. |
| Motion | Panels slide in like a turning page in about 0.2 seconds. Rewards count up with a sound and a light vibration. Nothing blocks input for more than half a second. |
| Layout | Currencies always top right. Back always top left. Minimum touch target 48 dp. Respect the safe area for notches and rounded corners. Reference resolution 1920 by 1080, scaling by height. |
| Technology | Unity UI (uGUI) with TextMesh Pro, sprite atlases per screen group, one shared prefab library for buttons, cards and bars so every screen is assembled from the same parts. |

## 13. Visual quality plan

The goal is very high quality graphics that still run well on mobile. On phones, quality comes mostly from lighting, colour, atmosphere and animation, not from polygon count. The plan spends the performance budget where the player looks: the animal in the scope, the weapon in hand, and the light in the scene.

### Art direction

- **Grounded realism with warm, golden-hour colour.** Real proportions, slightly rich colour, strong morning and evening light, mist in the low ground.
- **One world from many sources.** Assets will come from Meshy, Sketchfab, the current game and hand work. Four things make them look like one game: a shared colour palette applied in Blender or by a master material, one texture density (about 512 pixels per metre for important props, 256 for background), the same lighting and colour grade over everything, and a reject rule: an asset that still stands out after those three is not used.
- **A style sheet first.** Before production, generate 6 to 8 reference images (farm, each region at dawn, a predator, a weapon, a UI screen). Every Meshy prompt and image prompt after that refers back to them.

### Rendering set-up

| Area | Choice |
|---|---|
| Pipeline | Universal Render Pipeline (URP), linear colour, Vulkan with OpenGL ES 3 fallback, SRP Batcher on. Use Unity 6's GPU Resident Drawer and GPU occlusion culling on devices that support them; they help most in scenes full of instanced vegetation. |
| Lighting | Baked global illumination with light probes for moving objects, one real-time directional light (sun or moon) with a short shadow distance, baked reflection probes. Each time-of-day preset is a set of baked data plus a sky, a fog colour and a colour grade. |
| Canopy light | A light cookie on the sun gives moving dappled shade under trees for almost no cost. This single trick does a lot for a jungle. |
| Fog and depth | Distance fog plus height fog, tinted per time of day. Fog adds mood and also hides the draw distance, so the far terrain can be very cheap. |
| Vegetation | Grass and small plants drawn with GPU instancing and a wind shader. Trees with 3 levels of detail and a flat billboard for the far distance. Transparent leaf layers are the main cost in a jungle, so limit how many overlap and use solid leaf clusters at distance. |
| Water | A simple shader: scrolling normals, depth-based colour and soft edges. No real-time reflection except on the High tier. |
| Animals | The highest budget in the scene, because they are seen through a scope. Fur is done with good normal maps and a few hair cards on manes and tails (High tier only). |
| First-person weapon | Drawn by its own overlay camera so it never clips into the world and can have its own field of view. Recoil, sway and breathing are driven by code, which keeps the hand-made animation list short. |
| Anti-aliasing and scaling | MSAA on Medium and High, FXAA on Low. Render scale below 1.0 on Low and Medium with an upscaler, UI always drawn at full resolution. |
| Heat | Unity's Adaptive Performance package lowers render scale and effects when the phone gets hot, instead of letting the frame rate collapse. |

### Post-processing

One URP volume profile per time of day, blended when time changes, plus short local overrides for moments.

| Effect | Use | Tiers |
|---|---|---|
| Tonemapping and colour grade | The main source of the "expensive" look. A colour lookup table per time of day. | All |
| Bloom | Low intensity, for sun through leaves, muzzle flash, lanterns, fireflies. | Medium, High |
| Vignette | Subtle always; tightens while holding breath to focus the eye. | All |
| Depth of field | Only in the scope view, clue inspection, cut scenes and the kill camera. | High; faked with a blurred mask on others |
| Ambient occlusion | Baked into lightmaps for everyone; screen-space AO only on High. | High |
| Motion blur, film grain, chromatic aberration | Off. They cost frame time and hurt clarity on small screens. | None |

### Cinemachine

| Camera moment | How Cinemachine is used |
|---|---|
| Predator reveal | The current roar zoom-in, rebuilt as a short dolly move with handheld noise, then a blend back to the player. Used for rescue hunts and first sightings only. |
| Kill camera | A follow camera on the bullet with slowed time, cutting to an impact angle. Impulse adds a small shake. |
| Farm home screen | A slow drifting camera with drag-to-pan limits; tapping a building blends to a framed close-up. |
| Clue inspection | A short push-in to the track on the ground with a shallow focus, then back. |
| Scope and binoculars | Blends between hip, aim and scope cameras with their own fields of view. |
| Charge and recoil | Impulse sources on the weapon and on charging animals. |
| Cut scenes and legend intros | Timeline with Cinemachine shots, built once per legend predator, always skippable. |
| Result screen | A hero shot of the hunter at the scene at the current time of day, used as the background of the result panel. |

### VFX list

Built with Unity's Particle System, which runs on every phone. VFX Graph needs compute shader support that low-end devices lack, so it is not used for anything gameplay depends on.

- **Weapons:** muzzle flash, barrel smoke, shell eject, bullet trail for the kill camera, impact dust by surface, a light blood puff (can be switched off), arrow trail.
- **Tracking:** Hunter Sense pulse and clue glow (an outline shader, not particles), scent trail the dog follows, flies over a kill site, dust kicked from fresh tracks.
- **Wind and weather:** drifting leaves and pollen that show wind direction, grass sway, rain with ground splashes, puddle ripples, low mist cards, breath vapour in the highlands, lightning flash.
- **Life:** birds bursting from cover, butterflies, fireflies, campfire with sparks, chimney smoke on the farm, water splashes at crossings.
- **UI and farm:** coin and gem bursts, upgrade complete sparkle, building dust during construction, star stamps on the result screen.

### Quality tiers and budgets

The game picks a tier on first launch from the phone's memory and graphics chip, and the player can change it. These numbers are planning budgets to confirm on real devices in phase 0 and phase 1.

| Setting | Low | Medium | High |
|---|---|---|---|
| Typical phone | 3 GB memory, entry-level chip | 4 to 6 GB, mid-range chip | 8 GB or more, recent flagship chip |
| Frame rate target | 30 | 30, option for 45 | 60 |
| Render scale | 0.7 | 0.85 | 1.0 |
| Shadows | Baked only, blob shadow under animals | Real-time, 1 cascade, 25 m | Soft, 2 cascades, 50 m |
| Grass density | 30% | 60% | 100% |
| Textures | Half size | Full | Full |
| Post-processing | Tonemap, grade, vignette | Plus bloom | Plus depth of field and screen-space AO |
| Triangles on screen | 150,000 | 300,000 | 600,000 |
| Batches per frame | 150 | 250 | 400 |
| Memory in a hunt | 900 MB | 1.2 GB | 1.6 GB |

### Asset budgets

| Asset | Triangles (most detailed level) | Texture size | Levels of detail |
|---|---|---|---|
| Predator | 15,000 to 25,000 | 2048 | 3 |
| Livestock, dog | 6,000 to 10,000 | 1024 | 3 |
| First-person weapon and arms | 12,000 to 20,000 | 2048 | 1 |
| Farm building | 3,000 to 8,000 | 1024, shared atlas where possible | 2 |
| Tree | 2,000 to 5,000 | 1024 atlas shared by a species set | 3 plus billboard |
| Rock, log, prop | 300 to 2,000 | 512 to 1024 | 2 |

All textures compressed with ASTC (6×6 by default, 4×4 for UI and the weapon). Raw Meshy output is usually far above these numbers, which is why every model passes through Blender (section 15).

## 14. Audio plan and sound prompts

In this game sound is gameplay. A roar gives a direction, birds bursting from a bush give away a predator, and the player's own footsteps decide whether the animal runs. That is why the list is long.

### How audio is set up

- **Mixer groups:** Music, Ambience, Animals, Weapons, Player, UI, Voice. Each has its own volume, and settings expose Music, Sound and Voice sliders.
- **Mixer snapshots:** Normal; Hold breath (ambience muffled, heartbeat up); Scope (wind and distant sounds slightly raised); Kill camera (everything slowed and low); Night; Menu.
- **3D sound:** animals, water, the dog and clue sounds are positioned in the world so players can find them by ear. Ambience and music are stereo.
- **Variation:** every repeated sound has 3 to 5 versions and a small random pitch change, so footsteps and shots never sound copied.
- **Adaptive music:** tracking music is three layers in the same key and tempo: a quiet base, a tension layer that fades in near the animal, and a percussion layer when the animal is alerted. If the generated layers do not line up, fall back to crossfading separate tracks.
- **Files:** ask the generator for WAV. In Unity: short effects load into memory, music and long ambience stream from disk, 3D sounds are mono, loops must be seamless. Name files by ID, for example `WPN-01_rifle_shot_a.wav`.

### How to use the prompts

Each row is one request to the AI sound tool, which the owner runs. Add this ending to every sound-effect prompt: *"high quality, clean recording, no music, no human voices, no background noise unless described."* "Loop" means ask for a seamless loop. Where a row says "× 4", generate that many versions. If the tool has a length limit or works better with shorter prompts, the agent reformats the list for that tool (decision 18).

#### Ambience (stereo loops, 60 seconds unless stated)

| ID | Where it plays | Prompt |
|---|---|---|
| AMB-01 | Farm, day | Calm small farm at the edge of a forest in daytime, light breeze, a few songbirds, distant goats and chickens, soft wooden creaks, seamless loop |
| AMB-02 | Farm, night | Quiet farm at night, crickets, a distant owl, light wind in trees, an occasional sleepy animal shifting, seamless loop |
| AMB-03 | Meadow, day | Open meadow, steady light wind through tall grass, larks and distant crows, soft insect hum, wide and peaceful, seamless loop |
| AMB-04 | Pine forest, day | Cool pine forest, wind in high branches, sparse birds, a woodpecker far away, trunks creaking, seamless loop |
| AMB-05 | Dense jungle, day | Dense tropical jungle, layered exotic bird calls, cicadas, distant monkeys, water dripping from leaves, humid and close, seamless loop |
| AMB-06 | Jungle and forest, night | Jungle at night, frogs, night insects, occasional far-off animal call, tense but natural, seamless loop |
| AMB-07 | Rocky highlands | High rocky mountainside, strong gusting wind over bare rock, a distant eagle cry now and then, small stones shifting, empty and wide, seamless loop |
| AMB-08 | Dry grassland | Hot dry grassland at midday, cicadas, dry grass rustling in light wind, distant doves, seamless loop |
| AMB-09 | Dawn layer, any region | Dawn chorus of many birds waking up, building slowly, fresh and hopeful, seamless loop |
| AMB-10 | Rain layer, any region | Steady medium rain falling on leaves and soft ground, no thunder, seamless loop |
| AMB-11 | Thunder, random | Distant rolling thunder, 6 seconds, natural tail × 4 |
| AMB-12 | Stream or waterhole (3D) | Small shallow stream running over stones, close perspective, 30 second seamless loop |
| AMB-13 | Wind change cue | A single gust of wind rising and falling through trees, 5 seconds × 4 |
| AMB-14 | Campfire (3D) | Small campfire crackling with occasional pops, close, 30 second seamless loop |

#### Predators

Use this template and fill it from the table: *"[species] [call], realistic wildlife field recording, [distance], natural outdoor space, [length] × [versions]."*

| ID | Species | Calls to generate (call, distance, length, versions) |
|---|---|---|
| PRD-01 | Wolf | Long lonely howl, far, 6 s × 3 · pack howling together, far, 10 s × 2 · low warning growl, close, 3 s × 3 · snarling attack bark, close, 2 s × 3 · pained yelp, medium, 1 s × 3 · panting while running, close, 4 s loop |
| PRD-02 | Lion | Full deep roar sequence ending in grunts, far, 8 s × 2 · same roar, close and powerful, 6 s × 2 · low rumbling growl, close, 4 s × 3 · attack snarl, close, 2 s × 3 · hurt grunt, medium, 1.5 s × 2 |
| PRD-03 | Leopard | Rasping sawing call like a saw cutting wood, repeated, medium, 6 s × 2 · hiss and snarl, close, 2 s × 3 · low throat growl, close, 3 s × 3 · hurt yowl, medium, 1.5 s × 2 |
| PRD-04 | Bear | Deep bellowing roar, medium, 4 s × 3 · warning huff and jaw popping, close, 3 s × 3 · heavy slow breathing, close, 5 s loop · pained groan, medium, 2 s × 2 |
| PRD-05 | Hyena | Rising whoop call, far, 5 s × 3 · nervous giggling laugh, medium, 4 s × 3 · growl, close, 3 s × 2 |
| PRD-06 | Jackal | High yipping howl, far, 5 s × 3 · sharp warning bark, medium, 1 s × 3 · yelp, medium, 1 s × 2 |
| PRD-07 | Fox | Sharp single bark, medium, 1 s × 3 · eerie scream call at night, far, 3 s × 2 |
| PRD-08 | Wild boar | Low grunts while feeding, close, 4 s × 3 · angry charging snort, close, 2 s × 3 · squeal, medium, 2 s × 2 |
| PRD-09 | Lynx, mountain cat | Harsh yowl, medium, 3 s × 2 · hiss, close, 1.5 s × 3 |
| PRD-10 | Any large animal | Heavy body falling onto grass, 1.5 s × 3 · large animal trotting through dry leaves, 4 s loop · large animal running on soft ground, 4 s loop · animal sniffing the ground, close, 3 s × 2 · animal chewing and feeding, soft and not graphic, 4 s loop |

#### Livestock and the dog

| ID | Where it plays | Prompt |
|---|---|---|
| LIV-01 | Goats, calm | A goat bleating calmly on a farm, single bleat, close, 1.5 seconds × 4 |
| LIV-02 | Goats, raid | Frightened goats bleating in panic with hooves scrambling, 4 seconds × 3 |
| LIV-03 | Sheep | A sheep baa, calm, close, 1.5 seconds × 3 · panicked flock, 4 seconds × 2 |
| LIV-04 | Cows | A cow mooing, calm, medium distance, 2 seconds × 3 · distressed loud moo, 2 seconds × 2 |
| LIV-05 | Chickens | Hens clucking quietly, 5 second loop · chickens squawking in alarm with flapping wings, 3 seconds × 3 · rooster crow at dawn, 3 seconds × 2 |
| LIV-06 | Horses | Horse snort, 1 second × 3 · horse neigh, 2 seconds × 2 · nervous hoof stamping on dirt, 2 seconds × 2 |
| DOG-01 | Dog alert (raid, danger) | Medium-sized farm dog barking a sharp warning, three barks, close, 2 seconds × 3 |
| DOG-02 | Dog finds a clue | Dog sniffing the ground quickly then one short excited bark, 3 seconds × 3 |
| DOG-03 | Dog senses predator near | Dog growling low and steady, close, 3 seconds × 2 · dog whining softly, 2 seconds × 2 |
| DOG-04 | Dog happy (farm, reward) | Happy dog panting and one playful bark, 3 seconds × 2 · puppy yapping playfully, 2 seconds × 3 |

#### Nature clues (3D, heard from a direction)

| ID | Where it plays | Prompt |
|---|---|---|
| CLU-01 | Predator moves in cover | A flock of small birds suddenly bursting out of a bush, fast wing flaps and alarm chirps, 3 seconds × 3 |
| CLU-02 | Predator near, daytime | A single bird giving a sharp repeated alarm call from a tree, urgent, 3 seconds × 3 |
| CLU-03 | Predator near, jungle | Monkeys giving loud alarm barks from the treetops, medium distance, 4 seconds × 2 |
| CLU-04 | Non-target animal spooked | A deer alarm snort followed by hooves bounding away through undergrowth, 3 seconds × 2 |
| CLU-05 | Something moved | A dry twig snapping in a quiet forest, medium distance, 1 second × 4 |
| CLU-06 | Animal moving in brush | A large animal pushing slowly through thick bushes, leaves rustling, 3 seconds × 3 |
| CLU-07 | Kill site | Flies buzzing in a small swarm, close, 10 second seamless loop |
| CLU-08 | Vultures or crows over a kill | Several crows cawing and circling overhead, far, 5 seconds × 2 |

#### Player

| ID | Where it plays | Prompt |
|---|---|---|
| PLY-01 | Footsteps, grass | Single footstep of a man in boots on grass, close, dry, no echo, 0.5 seconds × 8 |
| PLY-02 | Footsteps, dry leaves | Single footstep on dry crunchy leaves and small twigs, close, 0.5 seconds × 8 |
| PLY-03 | Footsteps, mud | Single footstep in wet mud with a soft squelch, close, 0.5 seconds × 6 |
| PLY-04 | Footsteps, rock | Single boot step on rock and loose gravel, close, 0.5 seconds × 6 |
| PLY-05 | Footsteps, shallow water | Single step wading through ankle-deep water, splash, 0.7 seconds × 6 |
| PLY-06 | Footsteps, wood (farm, tower) | Single boot step on old wooden boards, slight creak, 0.5 seconds × 6 |
| PLY-07 | Moving, crouching | Soft rustle of a canvas jacket and leather straps as a person moves, 1 second × 4 |
| PLY-08 | Hold breath | A man taking one slow deep breath in and holding it, close, 2 seconds × 2 · slow controlled exhale, 2 seconds × 2 |
| PLY-09 | Tension, aiming | Human heartbeat, slow and steady, low and muffled, seamless loop at 60 beats per minute · same, fast at 110 beats per minute |
| PLY-10 | Player hit by a charge | A man grunting from a heavy impact and falling on grass, 1.5 seconds × 2 |
| PLY-11 | Pushing through bushes | A person brushing through leafy bushes, 1.5 seconds × 4 |

#### Weapons

| ID | Where it plays | Prompt |
|---|---|---|
| WPN-01 | Hunting rifle shot | Single hunting rifle gunshot outdoors, powerful sharp crack with a punchy low end, close perspective, short tail, 1.5 seconds × 4 |
| WPN-02 | Shot echo, open land | Echo tail of a single rifle shot rolling across a wide open valley, no initial crack, 4 seconds × 2 |
| WPN-03 | Shot echo, forest | Short dense echo of a rifle shot inside a thick forest, no initial crack, 2 seconds × 2 |
| WPN-04 | Bolt action | Bolt-action rifle bolt lifted, pulled back and pushed forward, crisp metal, close, 1.2 seconds × 3 |
| WPN-05 | Rifle reload | Loading three cartridges one by one into a rifle, metallic clicks, close, 3 seconds × 2 |
| WPN-06 | Empty weapon | Dry click of a rifle trigger with no round, close, 0.3 seconds × 2 |
| WPN-07 | Bolt-action rifle shot | Heavy large-calibre rifle shot, deeper and louder than a normal rifle, long powerful boom, 2 seconds × 3 |
| WPN-08 | Shotgun | Pump shotgun blast outdoors, wide heavy boom, close, 1.5 seconds × 3 · shotgun pump action, 0.8 seconds × 3 |
| WPN-09 | Bow | Wooden bow being drawn, string and limb creaking under tension, 1.5 seconds × 3 · bow release with string twang and arrow whoosh, 1 second × 3 |
| WPN-10 | Arrow hit | Arrow hitting soft ground with a thud, 0.5 seconds × 3 · arrow hitting a tree trunk, 0.5 seconds × 3 |
| WPN-11 | Crossbow | Crossbow firing, mechanical snap and bolt whoosh, 0.8 seconds × 3 · crossbow being cocked, ratchet clicks, 2 seconds × 2 |
| WPN-12 | Tranquilizer rifle | Air-powered dart rifle firing, soft compressed-air puff, 0.6 seconds × 3 |
| WPN-13 | Kill camera bullet | Slow-motion bullet flying past, deep stretched whoosh with air ripple, 3 seconds × 2 |
| WPN-14 | Miss, by surface | Bullet hitting dirt with a spray of soil, 0.6 seconds × 3 · bullet hitting a tree, wood splintering, × 3 · bullet ricochet off rock, × 3 |
| WPN-15 | Hit on animal | Dull heavy thud of an impact on a large body, not wet, not graphic, 0.4 seconds × 4 |
| WPN-16 | Shell on ground | Single brass cartridge case dropping onto dirt and small stones, 0.8 seconds × 3 |
| WPN-17 | Scope, raise weapon | Soft mechanical click and glass focus sound of a rifle scope zooming, 0.4 seconds × 2 · rifle being raised to the shoulder, cloth and wood, 0.6 seconds × 3 |
| WPN-18 | Time slows, time returns | Cinematic whoosh pitching downward as time slows, 1.5 seconds · reverse whoosh pitching upward as time returns, 1 second |

#### Tools and tracking

| ID | Where it plays | Prompt |
|---|---|---|
| TL-01 | Binoculars | Binoculars lifted on a leather strap, soft rattle, 0.6 seconds × 2 · quiet soft tick when an animal is tagged, 0.2 seconds |
| TL-02 | Trail camera placed | Strapping a small plastic device to a tree, buckle click then one soft electronic beep, 1.5 seconds |
| TL-03 | Camera photos reveal | Camera shutter click followed by a photo sliding onto a table, 1 second × 3 |
| TL-04 | Trap | Setting a metal spring trap, ratchet and click, 2 seconds · trap snapping shut, sharp metal clank, 0.5 seconds × 2 |
| TL-05 | Bait placed | A heavy cloth sack dropped on the ground, 0.8 seconds × 2 |
| TL-06 | Predator call | Hunter's reed call imitating a small animal in distress, thin wavering squeal, 3 seconds × 3 |
| TL-07 | Wind checker | Soft puff of powder from a small squeeze bottle, 0.4 seconds × 2 |
| TL-08 | Hunter Sense | Soft low pulse with an airy shimmer, like a deep breath of the forest, natural not science fiction, 1.5 seconds |
| TL-09 | Clue found | Short warm two-note wooden marimba chime with a pencil tick, 0.8 seconds |
| TL-10 | Clue inspected, guide entry | Pencil sketching quickly on paper, 1.5 seconds × 2 · paper journal page turning, 0.6 seconds × 3 |
| TL-11 | Map opened | A folded paper map being opened, 0.8 seconds × 2 |

#### Farm

| ID | Where it plays | Prompt |
|---|---|---|
| FRM-01 | Collect income | A handful of coins dropping into a leather pouch, 1 second × 3 |
| FRM-02 | Building in progress | Hammering nails and sawing wood on a small building site, 6 second seamless loop |
| FRM-03 | Building finished | Three final hammer hits then a short warm flourish of acoustic guitar and wood blocks, 2.5 seconds |
| FRM-04 | Gate, pen | Old wooden farm gate creaking open, 1.5 seconds × 2 · gate closing with a latch, 1.2 seconds × 2 |
| FRM-05 | Fence repair | Hammer driving a wooden fence post, four hits, 2 seconds × 2 |
| FRM-06 | Workshop craft | Blacksmith hammer on an anvil, three ringing hits then a metal file, 2.5 seconds × 2 |
| FRM-07 | Feeding | Grain pouring from a scoop into a wooden trough, 2 seconds × 2 |
| FRM-08 | Raid alarm | Urgent hand bell ringing three times with a dog barking behind it, 2.5 seconds |
| FRM-09 | Trader arrives | A cart with wooden wheels rolling up and stopping, small bell jingle, 3 seconds |

#### UI

| ID | Where it plays | Prompt |
|---|---|---|
| UI-01 | Button tap | Soft short knock on wood, clean, 0.1 seconds × 2 |
| UI-02 | Back, close | Lower softer wooden tap, 0.1 seconds |
| UI-03 | Tab switch, panel open | Single thick paper page flip, 0.4 seconds × 2 |
| UI-04 | Purchase success | Coins falling into a pouch with a bright warm chime, 1.5 seconds |
| UI-05 | Chest open | Wooden chest lid creaking open with a soft sparkle shimmer, 1.5 seconds |
| UI-06 | Stars on result screen | A rubber stamp hitting paper with a small chime, three versions each higher in pitch, 0.5 seconds each |
| UI-07 | Level up | Short triumphant fanfare with acoustic guitar, hand drum and wooden flute, 3 seconds |
| UI-08 | Not allowed, not enough coins | Dull muted double thump on wood, gentle, 0.3 seconds |
| UI-09 | Reward counting up | Fast light ticking of small coins, 2 second seamless loop |
| UI-10 | New item or mission | Small soft brass bell, single ring, 0.8 seconds |
| UI-11 | Hunt start | Low frame drum hit with a short hunting horn note, 2 seconds |
| UI-12 | Hunt success | Warm rising phrase on acoustic guitar and strings ending on a satisfied chord, 4 seconds |
| UI-13 | Hunt failed | Gentle falling phrase on solo guitar, thoughtful not sad, 3 seconds |

#### Music (no vocals)

| ID | Where it plays | Prompt |
|---|---|---|
| MUS-01 | Farm and main theme | Warm hopeful rural theme, fingerpicked acoustic guitar, bamboo flute melody, soft hand percussion, light strings, 90 BPM, D major, loopable, 90 seconds |
| MUS-02 | Tracking, base layer | Sparse ambient tension bed, low sustained strings and a soft drone, lots of silence so nature sounds stay clear, 80 BPM, D minor, loopable, 60 seconds |
| MUS-03 | Tracking, close layer | Same key and tempo as a D minor 80 BPM bed: soft pulsing frame drum and plucked low strings, slowly building tension, loopable, 60 seconds |
| MUS-04 | Animal alerted or charging | Urgent low drums and fast shakers, tense strings, D minor, 160 BPM, loopable, 40 seconds |
| MUS-05 | Legend hunt | Dark slow theme for an old dangerous predator, deep drum, solo low flute, distant choir-like pad without words, 70 BPM, loopable, 90 seconds |
| MUS-06 | Night hunt | Quiet uneasy night music, soft high strings, occasional low piano note, wide and empty, loopable, 60 seconds |
| MUS-07 | Store and menus | Light friendly variation of a rural acoustic theme, guitar and hand claps, relaxed, 100 BPM, loopable, 60 seconds |
| MUS-08 | Weekend event | Energetic folk percussion and strings, festive and driving, 120 BPM, loopable, 60 seconds |
| MUS-09 | Kill camera | Single deep cinematic drum hit with a long low tail, 3 seconds |

#### Voice (optional, see decision 16)

The mentor needs about 40 short lines, always with subtitles. Voice prompt: *"Older man in his sixties, calm, weathered and warm, speaks slowly and quietly as if not to scare an animal."* Example lines: "Fresh tracks. He passed this way within the hour." · "Check the wind before you take another step." · "Easy. Breathe out, then squeeze." · "Good shot. The herd is safe tonight."

## 15. Production pipeline and tools

This section is the working method. The short version: the agent builds inside Unity and Blender and drives the web tools the owner logs into; the owner approves style, generates sound from the prompts, and playtests on real phones.

### Tools and what each is for

| Tool | Use it for | Limits to plan around |
|---|---|---|
| Unity with Unity MCP | Scene building, scripts, animator controllers, Timeline and Cinemachine, particle effects, lighting, UI assembly, builds | The editor must be open with the MCP bridge running. The agent can read screenshots and logs, but game feel on a touch screen needs the owner to play a build on a phone. |
| Meshy.ai (owner logs in, the agent drives it through the browser) | Props, farm buildings, rocks, tools, base meshes and textures for animals and characters. Its own pages say its auto-rigger handles two-legged and four-legged characters. | Raw output is dense and uneven and always needs Blender cleanup. Weak at first-person weapons with moving parts and at foliage. Licence depends on the owner's plan (see below). Credits run out, so hero assets come first. |
| Sketchfab (owner's account) | Assets where AI generation is weakest: animated animals, weapons, vegetation, photo-scanned rocks | Only some licences are usable. Style varies between authors. Sketchfab is moving toward Fab, and the Unity plugin's current state is unconfirmed, so test it in phase 0. The fallback is downloading files from the site by hand. |
| Blender | Clean-up, polygon reduction, levels of detail, texture resize and baking, scale and pivot fixes, splitting weapons into moving parts, rig repair, retargeting animations, FBX export | Run by script, so every fix is repeatable. Hand sculpting or hand-keyed animation is slow and should be the last resort. |
| Mixamo (owner logs in) | Rigging and animations for the farmer, the mentor and any other human | **Humans only.** It cannot rig or animate four-legged animals. It also has no first-person weapon-handling animations. |
| Image generation (owner logs in) | Style sheet, UI art and icons, field guide sketches, loading art, store graphics, reference images for Meshy's image-to-3D | Text inside images is unreliable, so all text is added in Unity. Icons need a batch template to match. |
| AI sound tool (owner runs it) | Everything in section 14 | The agent cannot listen to results. The owner is the ear and rejects anything that sounds synthetic, especially animal calls. |
| Playwright MCP | A second way to drive web pages when the main browser tool is not suitable, and for repetitive web steps | Needs its own login session. |
| Poly Haven and ambientCG (suggested addition) | Free ground, bark and rock textures and sky images under the CC0 licence, with no credit required | Photographic, so they need the same colour treatment as everything else. |
| Git with Git LFS | Version control for the project and all source assets | Needed from day one. An MCP-driven workflow changes many files quickly, and every step must be reversible. |

### The path every 3D asset follows

1. **Brief:** style sheet image, size, budget
2. **Source:** Meshy, Sketchfab or the current game
3. **Blender:** scale, pivot, reduce, LODs, textures
4. **Rig, animate:** Meshy, Mixamo or retarget
5. **Unity:** prefab, material, colliders, LOD group
6. **Gate:** budget, style, phone test, licence log

No asset enters a game scene before step 6. An asset that fails the gate goes back to step 2 or is dropped.

### Where each asset group comes from

| Asset group | At relaunch | First choice | Fallback and notes |
|---|---|---|---|
| Predators | 8 species | The current game's models, if they pass the quality gate, because they are already rigged and animated | Sketchfab animated models, then Meshy with its four-legged rig |
| Livestock | 5 types | Meshy or Sketchfab | They need only a few animations: idle, graze, walk, panic run |
| Hunting dog | 1, puppy and adult | Sketchfab animated dog | Shares the dog-family skeleton with the wolf, so animations can be reused |
| Farmer and mentor | 2 humans | Meshy model, Mixamo rig and animations | Seen on the farm and in cut scenes only |
| First-person arms and weapons | 5 weapon types | Sketchfab weapon models, split into moving parts in Blender | The hardest group for AI tools. Recoil and sway come from code; reload and bolt animations are keyed by hand. |
| Farm buildings | 8, each with 3 upgrade looks | Meshy from style sheet images | A strong fit for Meshy |
| Props (fences, carts, tools, traps, cameras, crates) | About 60 | Meshy | Batch-generated from one prompt template |
| Rocks, logs, stumps | About 25 | Sketchfab CC0 photo scans, reduced in Blender | Meshy |
| Trees, bushes, grass | About 30 | Sketchfab game-ready foliage | Built in Blender. Not Meshy: generated foliage is too heavy for mobile. See decision 21 about paid packs. |
| Ground and sky | Per region | Unity Terrain with CC0 textures and sky images | Image generation for special ground such as track mud |
| Track and clue decals | 8 species, fresh and old, plus scat, claw and drag marks | Drawn from real references with image generation, then checked by hand | Accuracy matters here (section 6) |

### Animal animation plan

This is the largest art risk. A predator needs about 15 clips: idle, look around, walk, trot, run, stalk, sniff, eat, drink, freeze on alert, roar or call, attack, hit reaction, limp, death. Eight species would mean about 120 clips if each were made separately. Three decisions keep this manageable:

1. **Three skeleton families, not eight rigs.** Dog family (wolf, jackal, fox, hyena, the hunting dog), cat family (lion, leopard, lynx), and heavy animals (bear and boar, each on their own). Animations made for one family member are retargeted to the others in Blender, with speed and posture adjusted per species.
2. **Start from animals that already move well.** Order of preference: the current game's animated animals, then animated Sketchfab models with a usable licence, then Meshy's four-legged rig and animation library, and hand animation only for gaps.
3. **Prove one animal completely before making the rest.** The wolf goes through the whole pipeline in phase 1 and is judged through the scope on a real phone. If it does not look alive, the approach changes before seven more animals are built on it.

Hiding the joins helps too: blend trees for speed changes, small random head and ear movement, a tail driven by simple physics, and foot placement on slopes on the High tier.

### Licences and the credits screen

- **Sketchfab:** use only **CC0** and **CC BY** models. Do not use NonCommercial (the game earns money), NoDerivatives (the models are modified in Blender) or ShareAlike (its terms are awkward for a shipped game). Also avoid models that look ripped from another commercial game, which exist on Sketchfab whatever licence they claim.
- **Meshy:** its pricing page currently says Free-plan models are CC BY 4.0 (commercial use with credit, and the models are public) and paid plans give a private licence with no credit needed. Third-party pages disagree about the free plan, so check the live terms on the day work starts (decision 19).
- **Generated images and sounds:** check that the owner's plan on each tool allows commercial use.
- **Licence log:** one data file in the project lists every outside asset: name, author, link, licence, date, and what was changed. An asset without a log entry fails the quality gate.
- **Credits screen:** reached from Settings, built automatically from the licence log so it is never out of date. For each CC BY asset it shows the title, the author, the licence and that it was modified. It also lists fonts, sound sources and third-party software.

### Image prompts

Add this style line to every prompt so the results match: *"painterly realism, warm golden-hour light, earthy palette of forest green, bark brown, parchment and amber, clean readable shapes, no text, no watermark."* For icons add: *"single object centred on a plain flat light background, three-quarter view, hand-inked outline with flat colour fill, consistent line weight, 1024 by 1024."*

| ID | Use | Prompt (add the style line) |
|---|---|---|
| IMG-01 | Style sheet: the farm | A small family farm at the edge of a jungle valley at sunrise, wooden goat pen, thatched farmhouse, watchtower, mist in the low ground, forested foothills behind, wide landscape view |
| IMG-02 | Style sheet: regions (one each) | [open meadow / pine forest / dense jungle / rocky highlands / dry grassland] at dawn seen from a hunter's eye level, a faint animal trail leading into the distance, atmospheric depth, wide landscape view |
| IMG-03 | App icon | Close portrait of a wolf's face seen through a rifle scope crosshair, intense eyes, warm rim light, bold simple composition that reads at small size, square |
| IMG-04 | Store feature graphic and key art | A farmer with a rifle and a hunting dog standing guard in front of a goat pen at dusk, a lion's silhouette on a ridge behind, dramatic warm sky, wide 16:9, empty space on the left for a logo |
| IMG-05 | Loading screens (one per region) | A hunter kneeling to study a large paw print in mud in a [region], his dog beside him, early morning light through trees, cinematic wide 16:9 |
| IMG-06 | UI panel background | Seamless aged parchment paper texture, subtle fibres and light stains, even lighting, flat, tileable |
| IMG-07 | UI header and frame | Seamless dark weathered wooden plank texture, horizontal grain, even lighting, flat, tileable |
| IMG-08 | UI button | A blank stitched leather tag button, amber-brown, rounded rectangle, soft top light, plain flat background, front view |
| IMG-09 | Currency icons | Icon set: a stamped gold coin, a cut green gem, a bronze hunter's badge with a paw print (use the icon line) |
| IMG-10 | Material icons | Icon set: a bundle of wood planks, an iron ingot, a rolled piece of leather (use the icon line) |
| IMG-11 | Gear and tool icons | Icon of [binoculars / trail camera / spring trap / bait sack / reed call / hunting boots / camouflage jacket / rifle scope] (use the icon line, one per item) |
| IMG-12 | Building icons | Icon of a small rustic [goat pen / watchtower / workshop / hunter's lodge / kennel / vet hut / trading post] (use the icon line, one per building) |
| IMG-13 | Field guide animal pages | Naturalist's pencil and ink field sketch of a [species] in side view on aged paper, light watercolour wash, accurate anatomy, margin left empty for notes |
| IMG-14 | Field guide tracks and track decals | Accurate top-down ink drawing of a single [species] paw print at life proportions on plain paper, clean edges, scientifically correct toe and pad shape |
| IMG-15 | Mentor and dog portraits | Portrait of a weathered old forest ranger in his sixties with a grey moustache and a worn hat, kind eyes, shoulders up, plain background · portrait of a loyal medium-sized farm dog, alert ears, shoulders up, plain background |
| IMG-16 | Offer banners | A wooden crate packed with a rifle scope, coins and green gems, lit like a prize, 16:9 with empty space on the right · a rolled hunter's pass with a wax seal on a wooden table, 16:9 |
| IMG-17 | Store screenshot backgrounds | Generated in-game from real scenes, not with image tools, because store screenshots must show the real game |

### How a work session runs

1. The owner opens Unity with the MCP bridge and logs into the web tools needed that day.
2. Owner and agent agree the session goal from the roadmap (one feature or one batch of assets).
3. The agent builds it, checks it against the budgets, and commits it with a short note of what changed.
4. The session ends with a phone build and a short checklist of what to test.
5. The owner plays it and sends back what felt wrong. That feedback opens the next session.

**A feature is done when:** it runs at the target frame rate on the Low tier phone, its sounds and analytics events are connected, its UI follows the style guide, it has no errors in the log, and the owner has played it on a phone.

## 16. Content plan

The biggest risk in a rebuild is content cost. The answer is to build a few large, detailed maps and a **hunt generator** that creates many different hunts on each map by changing the predator's start point, trail path, clue positions, wind direction, weather and time of day. Three maps can then carry dozens of hunts that feel different.

| Content | At relaunch | 90 days later | Note |
|---|---|---|---|
| Regions (large maps) | 3 | 5 | Each map reused by many hunts, built to the standard in section 4 |
| Predator species | 8 | 12 | Reuse the old game's models where quality allows |
| Story hunts | 30 | 60 | About 10 to 14 days of story for a new player |
| Legend hunts | 2 | 5 | Each lasts several days |
| Weapons | 10 | 18 | Start with 5 distinct types |
| Livestock types | 5 | 7 |  |
| Farm buildings | 8 | 10 | Each with 3 visible upgrade levels |
| Events | 2 per month | 4 per month | Reskin the same event structure |

The counts are planning assumptions. A new player doing 2 to 3 story hunts a day uses 30 hunts in about 10 to 14 days, and daily hunts, collections and events fill the rest, so the relaunch has enough to reach the day 30 mark without new content.

### Content rules

- Reuse before creating: variants of an animal (color, size, scar) are cheaper than new species.
- Every region needs its own feel (terrain, sounds, weather) so that moving to it feels like progress.
- Write mission text and clues in a small, structured format so new hunts can be added as data, without code changes.
- Plan the first 30 hunts in a spreadsheet before building: region, animal, mission type, new idea introduced, difficulty.

## 17. Technical plan (Unity, Android)

The technical goal is simple: run smoothly on mid and low-end Android phones, let the game be changed without shipping a new build, and measure everything.

| Area | Plan | Why |
|---|---|---|
| Unity version | Current Unity 6 LTS in a new project, with URP from the first day. Stay on that LTS line for the whole project | Newer URP features help large vegetation scenes; an engine upgrade mid-project is expensive |
| Play requirements | Build with IL2CPP for ARM64. Meet the current target API level. Support 16 KB memory page sizes, including every native library inside ad and analytics SDKs. Check each upload in Play Console before relying on it | Google Play rejects uploads that miss these, and an out-of-date SDK is the usual cause |
| Download size | Android App Bundle with Play Asset Delivery, built through Addressables for Android. The base stays under Play's 200 MB limit (aim about 150 MB) and holds the whole first session. Later content comes in fast-follow and on-demand packs (see "Asset delivery" below) | Big downloads lose installs on slower connections and small phones |
| Performance | URP with the three quality tiers and budgets in section 13, level of detail, occlusion culling, instanced foliage, object pooling | Dense jungle maps are expensive; low-end phones must still play |
| Large maps | Terrain and object streaming in chunks, baked lighting | Keeps memory in check on 3 to 4 GB phones |
| Data-driven design | Animals, weapons, buildings, hunts and offers are data assets, not code. Balance values can be overridden from remote config | New content and tuning without programming or a new build |
| Animal AI | A state machine per animal with sight, hearing and smell checks, moving on a navigation mesh | Powers the tracking, wind and sound mechanics; easy to read and debug |
| Tracks and clues | Pooled footprint decals with an age value that changes their look | One system drives both visuals and the freshness clue |
| Hunt generator | Hunts defined as data (map, species, trail rules, weather, time) | Many hunts without new levels |
| Input | Unity's Input System with on-screen controls; layouts saved per player | Adjustable and left-handed controls |
| Remote config | Ad frequency, offer timing, reward values, event schedules, economy values | Change the economy in a day, with no update |
| Analytics | Event tracking for every step of the hunt chain, the store, and every ad placement, in place from the first prototype | D7 cannot be fixed without seeing where players leave |
| Purchases | Google Play Billing, with receipts checked on a server before gems are granted. Existing purchases restored | Protects the economy from fake purchases and honours old buyers |
| Saves | Local save plus cloud save with Google sign-in, versioned format. The game is playable offline; ads, store and cloud sync need a connection | Protects progress and allows migration |
| Ads | Mediation with at least two or three networks, consent form for regions that need it | Higher ad income, legal compliance |
| Privacy | Updated privacy policy and Play Data safety form covering analytics, ads and cloud save | Required for the listing; new SDKs change the answers |
| Localization | All text in string tables from the start; English first, then the top languages by installs | Adding languages later is cheap only if text was never hard-coded |
| Stability | Crash and ANR reporting, staged rollouts, a test list of low-end devices | Poor stability hurts both retention and store ranking |

A common backend choice for a small team is Firebase (analytics, remote config, crash reports, messaging, authentication, cloud functions), because it needs little server work.

### Project structure

- **Game code and content** in one top folder, split by feature (Hunt, Animals, Farm, Economy, Store, Ads, UI, Audio, Save, Analytics), each with its own assembly definition.
- **Data assets** (animals, weapons, buildings, hunts, offers) in a Data folder, one asset per item, so content is added without code.
- **Outside assets** in a ThirdParty folder, one subfolder per source asset, each matching an entry in the licence log.
- **Scenes:** a small Boot scene, the Farm scene, and one scene per region loaded on demand.
- **Docs folder:** this plan, the licence log, and a decisions log where every owner answer from section 21 is written down with its date.
- **Source assets** (Blender files, original downloads, generated images and sounds) are kept in the repository with Git LFS, outside the Unity Assets folder.

### Asset delivery

The game uses Google Play Asset Delivery, built with Unity's Addressables for Android package. Install-time packs are not used to make the download look smaller, because they still count toward the install the player waits for.

| Delivery | When it downloads | Contents |
|---|---|---|
| Base (in the bundle) | With the install | Code, Boot, Farm, the Meadow region, fox, jackal and wolf, UI, core audio. The whole first session, so a new player never waits. Under the 200 MB limit, aim about 150 MB |
| Fast-follow pack | Automatically, right after install | Region 2, the pine forest (needed on day 3) |
| On-demand packs | When the game asks for them | Region 3 onward, legend cinematics, event content, optional High-tier textures |

Rules from the first scene:

- One Addressables group per region and per pack.
- Region scenes load only through Addressables.
- Base content never references another region's assets directly.
- The Boot scene uses a content delivery service: it checks pack status, shows download progress in the UI style, handles offline play (decision 14), and shows the mobile data confirmation for large downloads.

Pack size limits (about 1.5 GB per pack) are checked against Google's current page before the content is split. If iOS (decision 4) or live events without a store update are needed later, a remote Addressables host (Unity CCD or Firebase) is added for live content only.

### Existing players

The update keeps the same application ID, so old save data is still on the phone. The new game reads it once and gives a veteran reward (for example a special rifle skin and some gems). Any paid entitlement the player holds carries over. A short "what's new" screen on first launch explains the change.

## 18. Store listing and relaunch marketing

The listing is the first screen of the game. A relaunch with the old icon and screenshots hides all the new work from new players.

- **New listing art at full launch:** icon, feature graphic, eight screenshots that tell the story in order (the raid, the track, the stalk, the shot, the farm, the dog, the legend, the store of gear), and a 30 second video made from real gameplay.
- **Test the icon and first screenshot** with Play Console store listing experiments before the rollout reaches 100%.
- **Keep the search keywords** that bring installs today when rewriting the title and description. Check them in Play Console before changing anything.
- **Ask for a rating at a good moment:** after a perfect shot or a finished legend hunt, through the Play in-app review prompt, never on launch.
- **Tell existing players:** release notes, the "what's new" screen and the veteran reward.
- **Paid installs only after the soft-launch minimums are met.** Buying users for a game that does not retain yet is the fastest way to lose money.

## 19. Roadmap and phases

The game is built by the owner and a coding agent using AI tools, not by a studio team. The roadmap starts with a set-up phase and leaves a real tuning window at soft launch. The week numbers are planning assumptions that depend mainly on how many hours a week the owner can give to review and phone testing. The order and the gates matter more than the dates.

| Phase | When | Work |
|---|---|---|
| 0. Set-up | Weeks 1 to 2 | Old project inventory; new project and tools; style sheet and UI guide; pipeline test assets |
| 1. Vertical slice | Weeks 3 to 10 | One region at final look; full hunt chain; wolf, fully animated; controls and analytics |
| 2. Farm and store | Weeks 11 to 18 | Farm home screen; economy and progression; store, ads and daily loop; story tutorial and saves |
| 3. Content | Weeks 19 to 26 | Regions 2 and 3; 8 predators and 30 hunts; 2 legend hunts; full audio and credits |
| 4. Soft launch | Weeks 27 to 32 | Closed test; limited release; weekly tuning; listing art tests |
| 5. Full launch | Week 33 onward | Staged rollout; hunter pass and events; regions 4 and 5; clubs and subscription |

Six phases, five gates. A gate sits after each of phases 0 to 4. Gate E, at the end of soft launch, is the one that decides the full launch.

| Gate | After phase | Pass when |
|---|---|---|
| A | 0. Set-up | The old project is inventoried: a keep-or-replace list of its assets, plus its application ID, product IDs, ad unit IDs and save format. Current retention and revenue numbers are recorded as the baseline. The new project builds to a phone, and a test upload to the internal test track is accepted by Play. That upload includes one test asset pack, and the pack downloads on a phone. One Meshy prop, one Sketchfab model and one animated animal have gone through the whole pipeline and run on a low-end phone inside budget. The owner has approved the style sheet and the UI style guide. |
| B | 1. Vertical slice | Five people who have never seen the game finish a full hunt without help and say they would play another. The slice holds 30 frames per second on the Low tier phone. The wolf looks alive through the scope. This is the most important gate before soft launch: if tracking is not fun here, more content will not fix it. |
| C | 2. Farm and store | A new player can play days 0 to 3 of the calendar in section 10 end to end. A test purchase and every rewarded ad placement work. Saves survive an app update and a phone change. |
| D | 3. Content | 30 story hunts and 2 legend hunts are playable in order. Download size, memory and frame rate are inside budget on all three test phones. Every outside asset is in the licence log and on the credits screen. All sounds from section 14 are in. |
| E | 4. Soft launch | The minimums in section 2 are met for two weekly cohorts in a row, crash-free users are above 99%, and the rating from test users is healthy. If the minimums are not met, stay in soft launch and tune. Do not scale content or buy installs before this gate. |

- **Why ads and the store are built in phase 2, not later:** the revenue data from the soft launch is needed to tune them.
- **Why the slice comes before the farm:** the farm only works if the hunt it feeds is fun.
- **What can run in parallel:** sound generation from section 14 and UI image generation from section 15 can start in phase 0, because they depend only on the style guide.

### Phase 0 task list

1. Inventory the old project without changing it: Unity version, assets worth reusing and their licences, application ID, in-app product IDs, ad unit IDs, SDK list, and where and how player progress is saved.
2. Record today's numbers from Play Console and the ad dashboard as the baseline (decisions 1 and 2).
3. Create the new Unity project (current Unity 6 LTS, URP, Android, IL2CPP, ARM64), its Git repository with LFS, and the folder structure in section 17.
4. Build an empty scene to a phone. Then upload a test build to the internal test track with the existing application ID and signing key, to prove Play accepts it. Include one small test asset pack (Play Asset Delivery) and check that it downloads on the phone.
5. Connect and test each tool: Unity MCP, Blender scripts, Meshy, Sketchfab (plugin or manual download), Mixamo, the image tool and the sound tool.
6. Add analytics, remote config and crash reporting to the empty project.
7. Produce the style sheet (IMG-01 and IMG-02 in section 15) and one sample UI screen in the style of section 12, for the owner to approve.
8. Take one Meshy prop, one Sketchfab model and one animated animal through the full asset path onto the Low tier phone.

## 20. KPIs, testing and risks

### What to measure

| Area | Metrics | Notes |
|---|---|---|
| Retention | D1, D7, D30 | Targets in section 2; always compare by install cohort |
| First session | Tutorial completion, first kill rate, time to first farm view, share who place the first trail camera | Find the exact step where new players leave |
| Engagement | Sessions per day, minutes per day, hunts per day, hunt abandon rate by step of the hunt chain | Abandon rate shows where tracking is too hard or too slow |
| Ads | Rewarded views per daily active user, opt-in rate per placement, interstitials per user | Watch together with retention, never alone |
| Purchases | Payer conversion, revenue per daily active user, first purchase day, offer conversion | Set targets after soft launch, using the game's real numbers |
| Revenue balance | Ad share and IAP share of revenue | Goal 50/50 at 12 months |
| Stability | Crash-free users, ANR rate, low-end device frame rate | Keep well below the store's bad-behavior thresholds |

### Testing plan

1. **Closed test** with a small group, focused on controls and tracking clarity.
2. **Soft launch** to a limited audience and compare their cohorts against the old game.
3. **A/B tests** run through remote config: first interstitial timing, interstitial gap, starter pack timing, number of tracking clues, assist level default, daily reward size.
4. **Weekly review:** one retention chart, one revenue chart, the top three drop-off points, and one change to try.

### Risks and answers

| Risk | Impact | Answer |
|---|---|---|
| Scope grows too large | The relaunch slips by months | Ship in phases; each phase must be playable and pass its gate |
| Tracking feels slow or confusing | Players quit in the first hunts | 3 to 5 clues, Hunter Sense, assist levels, watch the hunt abandon rate; proven at gate B before anything else is built |
| Animals look stiff or fake | The core fantasy fails in the scope | Skeleton families, start from animated sources, prove the wolf first (section 15) |
| Assets from different sources do not match | The game looks cheap and patched together | Style sheet, shared palette and texture density, one colour grade, the reject rule |
| A licence mistake | Takedown request or listing removal | Licence filter, licence log as part of the quality gate, automatic credits screen |
| A tool changes or runs out (credits, plugin, plan terms) | Production stops | Every asset group has a fallback source; hero assets are made first; tools are tested in phase 0 |
| The owner's review time becomes the bottleneck | Work piles up unapproved | Short sessions that each end in one phone build and one checklist |
| Too many ads | D1 and D7 drop | Rewarded first, interstitial caps, remote-config control |
| Weak purchases in low-income markets | Revenue stays ad-heavy | Regional pricing, low-price starter pack, payment methods that fit local players |
| Low-end phones cannot run big maps | Bad reviews, uninstalls | Quality tiers, streaming, budgets checked on a low-end phone every build |
| Store policy or rating problems with hunting content | Update rejected or listing limited | Keep violence light, answer the rating questionnaire honestly, read current policy before each submission |
| Upload rejected for a technical requirement | A week lost | Check target API and 16 KB support on a test upload in phase 0, not at launch |
| Existing players dislike the change | Rating drop | Soft launch away from the main audience, migration reward, "what's new" screen, staged rollout |

## 21. Owner decisions and defaults

These decisions belong to the owner. Each has a default, so the agent can start without waiting for every answer. The agent asks the starred ones at the start of phase 0, because phase 0 cannot finish without them, and writes every answer in the decisions log.

| # | Decision for the owner | Default the agent uses until the owner answers |
|---|---|---|
| **Business and data** | | |
| 1★ | What are the current D1, D7, D30, average session length, daily active users, installs per day and top five countries (Play Console)? | The targets in section 2 stay as written and the "Today" column stays empty |
| 2 | What is the current revenue per daily user from ads, and from purchases? Which ad networks and mediation are in use? | The worked example in section 11 stays illustrative |
| 3★ | How many hours a week can the owner give to reviewing work and testing on phones? | About 10 hours a week; the roadmap dates are based on that |
| 4 | Should the new game come to iOS later? | Android only for now; nothing in the build should block an iOS version later |
| 5 | Is there a budget for paid tools and assets, and roughly how much per month? | Free sources plus one paid Meshy plan |
| **The existing project** | | |
| 6★ | Where is the old project folder, and how will the agent get the signing key, the application ID and access to Play Console test tracks? | The owner provides them in phase 0; the agent never stores secrets in the repository |
| 7★ | Which animal models and animations in the old game does the owner hold licences for? | Each is checked in the phase 0 inventory; an unclear licence means it is not reused |
| 8 | Have players bought anything in the current game (for example remove-ads) that must keep working? | Yes, and every existing purchase is restored |
| 9 | Is there a Firebase project and a Google Play Games set-up already? | Both are set up new, and the new project gets its own Git repository |
| **Game design** | | |
| 10 | Does the game keep its current name, or does the relaunch get a new title? | Same name, new subtitle, decided with keyword data |
| 11 | How much blood is acceptable, and what age rating is the aim? | Light blood with an off switch, teen rating |
| 12 | Is the farmer a fixed character, or can the player choose a man or a woman? Is the farmer seen in third person on the farm? | Choice of two looks; first person in hunts, seen from outside on the farm |
| 13 | Is the setting in section 4 (fictional South Asian foothills) accepted, or is a different place or no fixed place preferred? | The setting in section 4 |
| 14 | Must the game be fully playable offline? | Hunts and farm work offline; ads, store and cloud save need a connection |
| 15 | Which languages at launch? | English, then the top languages from decision 1 |
| **Production** | | |
| 16 | Should the mentor have a voice (AI-generated), or text only? | Text with subtitles, plus about 40 short AI-voiced lines |
| 17 | Is AI-generated music acceptable, or are licensed or composed tracks wanted? | AI-generated from the prompts in section 14 |
| 18 | Which AI sound tool and which image tool will be used? Their prompt limits and licences differ. | Prompts stay in the general form written here |
| 19★ | Which Meshy plan is the owner on? It decides the licence and whether models need a credit. | Free plan: credited on the credits screen, licence checked on the day |
| 20★ | Which Android phones are available for testing? One low-end, one mid-range and one high-end are needed. | The owner names three, and the Low tier budget is set from the weakest |
| 21 | If free sources fall short for vegetation or animal animation, may paid Unity Asset Store packs be used? | Yes, as a fallback, after asking the owner each time |
| 22 | Who are the five outside playtesters for gate B? | Friends or family who have not seen the game |
| 23 | Who updates the privacy policy and the Play Data safety form? | The agent drafts both, the owner reviews and submits |
| 24 | Should the soft launch use an open test on the main listing or a second listing in test countries? | Decided from the installs per day in decision 1 |

## 22. Sources

Benchmark figures come from search summaries of the pages below and are approximate; check them against the originals before using them in a business case.

- [Playio: D1/D7/D30 retention benchmarks 2026](https://blog.playio.co/d1-d7-d30-retention-benchmarks-2026)
- [GameAnalytics benchmarks summary (InvestGame)](https://investgame.net/news/pdf/2026-01-20-mobile_retention_benchmarks_2026/)
- [Segwise: mobile game retention by genre 2026](https://segwise.ai/blog/mobile-gaming-app-user-retention-strategies.md)
- [Playio: hybrid-casual monetization and IAP/ads split by sub-genre](https://blog.playio.co/hybrid-casual-games-monetization)
- [Game Growth Advisor: hybrid monetization guide 2026](https://gamegrowthadvisor.com/blog/2026-06-02-hybrid-monetization-mobile-games-iap-ads-guide-2026/)
- [Sensor Tower mobile game ad monetization 2026 (summary)](https://gamedevreports.substack.com/p/sensor-tower-mobile-game-ad-monetization)

Tool and platform facts were checked on Nov 11, 2026, also from search summaries. Tool terms change often, so confirm each on the live page before relying on it.

- [Meshy: AI auto-rigging (two-legged and four-legged support)](https://www.meshy.ai/features/ai-auto-rigging)
- [Meshy: pricing and licence by plan](https://docs.meshy.ai/pricing)
- [Sketchfab: update now that Fab is live](https://sketchfab.com/blogs/community/sketchfab-update-what-you-need-to-know-now-that-fabs-live/)
- [Unity: engine support for 16 KB memory page sizes](https://discussions.unity.com/t/info-unity-engine-support-for-16-kb-memory-page-sizes-android-15/1589588)
- [Unity manual: Android requirements and compatibility](https://docs.unity.com/en-us/engine/6000.6/manual/platform-specific/android/introducing/requirements-and-compatibility)
