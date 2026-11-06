using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(-20)]
public sealed partial class CoinForgeUI : MonoBehaviour
{
    [SerializeField] Sprite[] artwork;
    const string PlayTimeKey = "ic02_foreground_seconds";
    const int MilestoneStep = 25;

    OfflineEarnings offline;
    double refreshAfter, offlineAmount, awaySeconds;
    double savedPlayTime, foregroundSince, nextTimeSave;
    bool started, foreground, adInFlight;
    double shownCoins = -1, shownIncome = -1, shownWarp = -1;
    int shownGems = -1;
    int quantity = 1; // zero selects Max
    Dialog dialog;

    enum Dialog { None, Offline, Prestige, Stats, Help }

    void OnEnable()
    {
        GameEvents.CoinsChanged += OnCoins;
        GameEvents.OfflineEarned += OnOffline;
        GameEvents.RewardedReady += OnAdReady;
        GameEvents.RewardGranted += OnReward;
        GameEvents.PrestigeDone += OnPrestige;
        if (started) BeginForeground();
        if (InTutorial) SuppressTutorialAds();
    }

    void Start()
    {
        Application.targetFrameRate = 60;
        offline = FindFirstObjectByType<OfflineEarnings>();
        double.TryParse(PlayerPrefs.GetString(PlayTimeKey), NumberStyles.Float,
            CultureInfo.InvariantCulture, out savedPlayTime);
        started = true;
        BeginForeground();
        Build();
        Reflow();
        CheckFirstTutorial();
        if (offline != null && offline.PendingAmount > 0)
            OnOffline(offline.PendingAmount, offline.AwaySeconds);
        Refresh();
    }

    void OnDisable()
    {
        GameEvents.CoinsChanged -= OnCoins;
        GameEvents.OfflineEarned -= OnOffline;
        GameEvents.RewardedReady -= OnAdReady;
        GameEvents.RewardGranted -= OnReward;
        GameEvents.PrestigeDone -= OnPrestige;
        ReleaseTutorialAds();
        EndForeground();
    }

    void OnApplicationPause(bool paused)
    {
        if (!started) return;
        if (paused) EndForeground();
        else
        {
            screenW = 0;
            if (InTutorial) HideTutorialBanner();
            BeginForeground();
        }
    }

    void OnApplicationQuit() => EndForeground();

    void BeginForeground()
    {
        if (foreground) return;
        foregroundSince = Time.realtimeSinceStartupAsDouble;
        nextTimeSave = foregroundSince + 30;
        foreground = true;
    }

    double PlayTime => savedPlayTime + (foreground ? Time.realtimeSinceStartupAsDouble - foregroundSince : 0);

    void EndForeground()
    {
        if (!foreground) return;
        savedPlayTime = PlayTime;
        foreground = false;
        SavePlayTime();
        PlayerPrefs.Save();
    }

    void SavePlayTime() => PlayerPrefs.SetString(PlayTimeKey, PlayTime.ToString("R", CultureInfo.InvariantCulture));

    void OnCoins(double _)
    {
        if (safe == null) return;
        // Immediate purchase events can arrive between the core's 10 Hz heartbeats.
        double now = Time.realtimeSinceStartupAsDouble;
        if (now < refreshAfter) return;
        refreshAfter = now + 0.1;
        Reflow();
        Refresh();
        RefreshTutorial();
        if (foreground && now >= nextTimeSave)
        {
            nextTimeSave = now + 30;
            SavePlayTime();
        }
    }

    void OnOffline(double amount, double seconds)
    {
        offlineAmount = amount;
        awaySeconds = seconds;
        if (modal != null) Open(Dialog.Offline);
    }

    void OnAdReady(bool ready)
    {
        if (!ready) adInFlight = false;
        if (safe != null) RefreshAdControls();
    }

    void OnReward(RewardType type)
    {
        if (type == RewardType.OfflineDouble && dialog == Dialog.Offline) CloseDialog();
    }

    void OnPrestige()
    {
        if (dialog == Dialog.Prestige) CloseDialog();
    }

    void ShowAd(RewardType type)
    {
        if (InTutorial || adInFlight || AdManager.I == null) return;
        if (type == RewardType.TimeWarp && !CanTimeWarp()) return;
        adInFlight = AdManager.I.ShowRewarded(type);
        RefreshAdControls();
    }

    bool CanTimeWarp() => AdManager.I != null && AdManager.I.TimeWarpReady
        && IdleEngine.I.SecondsToNextPurchase() > 180;

    void RefreshAdControls()
    {
        bool previewBoost = tutorialStep == TutorialSteps;
        bool ready = !InTutorial && !adInFlight && AdManager.I != null && AdManager.I.RewardedReady;
        SetAvailable(boostAd, ready, previewBoost);
        Set(boostAdStatus, ready || previewBoost ? "Watch ad" : "Loading…");
        SetAvailable(warpAd, ready);
        SetAvailable(offlineAd, ready);
        SetAvailable(offlineCollect, !adInFlight);
    }

    void Refresh()
    {
        var engine = IdleEngine.I;
        if (shownCoins != engine.Coins)
        {
            shownCoins = engine.Coins;
            Set(coins, BigNum.Format(shownCoins));
        }
        double rate = engine.IncomePerSec;
        if (shownIncome != rate)
        {
            shownIncome = rate;
            Set(income, "+" + BigNum.Format(rate) + " / sec");
        }
        if (shownGems != engine.Gems)
        {
            shownGems = engine.Gems;
            Set(gems, BigNum.Format(shownGems));
        }
        int boostSeconds = Mathf.CeilToInt((float)engine.BoostRemaining);
        if (boostSeconds != shownBoostSeconds)
        {
            shownBoostSeconds = boostSeconds;
            Set(boostState, boostSeconds > 0 ? "Boost " + BigNum.Time(boostSeconds) : "Boost: OFF");
            boostBadge.alpha = boostSeconds > 0 ? 1 : 0.58f;
        }

        RefreshAdControls();
        bool warp = CanTimeWarp();
        warpAd.gameObject.SetActive(warp);
        if (warp)
        {
            double reward = engine.BaseIncomePerSec * engine.timeWarpHours * 3600;
            if (shownWarp != reward)
            {
                shownWarp = reward;
                Set(warpAdStatus, "+" + BigNum.Format(reward) + " coins");
            }
        }

        for (int i = 0; i < rows.Length; i++) RefreshRow(i, engine);
        RefreshDialog();
    }

    void RefreshRow(int index, IdleEngine engine)
    {
        var upgrades = engine.Upgrades;
        var def = upgrades.Defs[index];
        int owned = upgrades.Owned[index];
        bool known = owned > 0 || engine.Coins >= def.baseCost * 0.5;
        int count = quantity == 0 ? upgrades.MaxAffordable(index, engine.Coins) : quantity;
        double cost = upgrades.CostOf(index, Mathf.Max(1, count));
        bool affordable = count > 0 && cost <= engine.Coins;
        Row row = rows[index];
        if (row.Known != known)
        {
            row.Known = known;
            row.Icon.enabled = known;
            row.Locked.enabled = !known;
            Set(row.Name, known ? def.name : "???");
        }
        if (row.Owned != owned)
        {
            row.Owned = owned;
            int progress = owned % MilestoneStep;
            row.Progress.anchorMax = new Vector2((float)progress / MilestoneStep, 1);
            Set(row.Milestone, BigNum.Format(progress) + "/" + BigNum.Format(MilestoneStep));
            Set(row.Count, "x" + BigNum.Format(owned));
        }
        double production = upgrades.ProdOf(index) * engine.PrestigeMult * (shownBoostSeconds > 0 ? 2 : 1);
        if (row.Production != production)
        {
            row.Production = production;
            Set(row.Rate, "+" + BigNum.Format(production) + " / sec");
        }
        if (row.PriceValue != cost || row.BuyCount != count)
        {
            row.PriceValue = cost;
            row.BuyCount = count;
            Set(row.Price, BigNum.Format(cost));
            Set(row.BuyLabel, quantity == 0 ? "Buy Max" : "Buy x" + BigNum.Format(count));
        }
        SetAvailable(row.Buy, affordable);
        row.Background.color = affordable ? ShopRow : MutedRow;
    }

    void Choose(int count)
    {
        if (InTutorial) return;
        quantity = count;
        for (int i = 0; i < modes.Length; i++)
        {
            bool selected = quantity == ModeCounts[i];
            modes[i].enabled = selected;
            modeLabels[i].color = selected ? Ink : Ivory;
        }
        foreach (var row in rows) row.BuyCount = -1;
        Refresh();
    }

    void Buy(int index)
    {
        if (InTutorial && (tutorialStep != 2 || index != 0)) return;
        var engine = IdleEngine.I;
        bool bought = quantity == 0 ? engine.BuyMax(index) : engine.Buy(index, quantity);
        if (InTutorial && bought) SetTutorialStep(3);
    }

    void Open(Dialog next)
    {
        if (InTutorial || (dialog == Dialog.Offline && next != Dialog.Offline)) return;
        dialog = next;
        modal.gameObject.SetActive(true);
        offlineCard.gameObject.SetActive(next == Dialog.Offline);
        prestigeCard.gameObject.SetActive(next == Dialog.Prestige);
        statsCard.gameObject.SetActive(next == Dialog.Stats);
        helpCard.gameObject.SetActive(next == Dialog.Help);
        RefreshDialog();
    }

    void OpenStats()
    {
        if (InTutorial) return;
        Open(Dialog.Stats);
        GameEvents.RaiseMilestoneMenu();
    }

    void CloseDialog()
    {
        dialog = Dialog.None;
        modal.gameObject.SetActive(false);
    }

    void Collect()
    {
        if (adInFlight) return;
        offline.Collect();
        CloseDialog();
    }

    void RefreshDialog()
    {
        var engine = IdleEngine.I;
        switch (dialog)
        {
            case Dialog.Offline:
                Set(offlineTime, "Away for " + BigNum.Time(awaySeconds));
                Set(offlineCoins, "+" + BigNum.Format(offlineAmount) + " coins");
                Set(offlineAdText, "Watch ad: x2  •  +" + BigNum.Format(offlineAmount * 2) + " coins");
                Set(offlineAdStatus, offlineAd.interactable ? "Reward after the ad" : "Loading…");
                break;
            case Dialog.Prestige:
                int pending = engine.PendingGems();
                Set(prestigeGems, BigNum.Format(engine.Gems) + "  →  " + BigNum.Format(pending) + " Gems");
                SetAvailable(prestigeConfirm, pending > engine.Gems);
                break;
            case Dialog.Stats:
                var upgrades = engine.Upgrades;
                int top = 0;
                for (int i = 1; i < upgrades.Owned.Length; i++)
                    if (upgrades.Owned[i] > upgrades.Owned[top]) top = i;
                Set(statsLifetime, BigNum.Format(engine.Lifetime) + " coins");
                Set(statsTime, BigNum.Time(PlayTime));
                Set(statsTop, upgrades.Owned[top] == 0 ? "None yet"
                    : upgrades.Defs[top].name + "  x" + BigNum.Format(upgrades.Owned[top]));
                break;
        }
    }

    static void Set(Text target, string value)
    {
        if (target.text != value) target.text = value;
    }
}
