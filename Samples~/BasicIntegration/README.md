# Basic integration

`SanbukDemo.cs` shows all three shapes an ad takes in a game:

- a **banner** laid over the game by the native SDK,
- an **in-scene ad** you draw yourself from plain data,
- a **rewarded** ad, with the reward granted in `onReward` and nowhere else.

Put it on an empty GameObject, fill in the codes from your Sanbuk panel, wire
the UI fields, and build for Android. In the editor everything answers "no ad"
on purpose — that is the path your game has to handle anyway.
