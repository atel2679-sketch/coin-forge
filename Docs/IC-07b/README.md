# IC-07b · UI polish

Verified on Redmi Note 7 at 1080×2340. Help topics size themselves to their wrapped text. The full Build description is visible, and Prestige is visible at the bottom of the scroll. The gold thumb moves down as the list scrolls, and the bottom fade clears at the end. The gold Help button retains the full 48dp hit area. Step 5 displays the green Boost preview with “Watch ad” while blocking taps.

- [How to Play at the top](how-to-play-top.png)
- [How to Play at the bottom](how-to-play-bottom.png)
- [Step 5](step-5.png)
- [Main screen after Got it](main-after-tutorial.png)

## Same-session ad verification

Each test started from fresh tutorial state. AdManager enabled as the tutorial closed. In both runs, the native test banner then loaded and Boost became interactable in the same Android process.

### Got it

```text
I/Unity   (23944): IC07_QA step=0 flag=1 adsEnabled=True banner=0 coins=0.8 owned=1
I/Unity   (23944): [Ads] Banner height px: 167
I/Unity   (23944): IC07B_QA same-session ready enabled=True banner=167 rewarded=True boostInteractable=True
```

### Skip

```text
I/Unity   (25201): IC07_QA step=0 flag=1 adsEnabled=True banner=0 coins=0.0 owned=0
I/Unity   (25201): [Ads] Banner height px: 167
I/Unity   (25201): IC07B_QA same-session ready enabled=True banner=167 rewarded=True boostInteractable=True
```
