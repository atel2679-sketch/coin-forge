using UnityEngine;
using UnityEngine.UI;

public sealed partial class CoinForgeUI
{
    const int FloatCapacity = 20;
    const float FloatDuration = 0.72f;
    const float EffectFps = 20;
    readonly FloatText[] floating = new FloatText[FloatCapacity];
    RectTransform coinArt;
    Image[] burstFrames, sparkleFrames;
    int nextFloat, liveFloats, burstFrame = -1, sparkleFrame = -1;
    float tapAge = 1;
    double lastTapValue = -1;
    string lastTapText;

    sealed class FloatText
    {
        public Text Label;
        public Vector2 Origin;
        public float Age;
    }

    void BuildTapEffects(Transform parent, RectTransform coin)
    {
        coinArt = coin;
        var effects = Box(parent, "Tap effects");
        // Keep animated sprites and labels on their own canvas.
        effects.gameObject.AddComponent<Canvas>();
        var bursts = Box(effects, "Coin burst flipbook");
        var sparkles = Box(effects, "Sparkle flipbook");
        Area(bursts, 0.01f, 0.02f, 0.62f, 0.94f);
        Area(sparkles, 0.21f, 0.04f, 0.56f, 0.59f);
        burstFrames = BuildFrames(bursts, "vfx_coin_burst_", 8);
        sparkleFrames = BuildFrames(sparkles, "vfx_sparkle_", 6);

        for (int i = 0; i < floating.Length; i++)
        {
            Text label = Label(effects, "", 55, Gold);
            Place(label.rectTransform, 0, 0, 320, 79);
            label.gameObject.SetActive(false);
            floating[i] = new FloatText { Label = label };
        }
    }

    Image[] BuildFrames(Transform parent, string prefix, int count)
    {
        var frames = new Image[count];
        for (int i = 0; i < count; i++)
        {
            frames[i] = Art(parent, prefix + (i + 1).ToString("00") + ".png");
            frames[i].enabled = false;
        }
        return frames;
    }

    void Tap()
    {
        double value = IdleEngine.I.TapValue;
        IdleEngine.I.Tap();
        if (value != lastTapValue)
        {
            lastTapValue = value;
            lastTapText = "+" + BigNum.Format(value);
        }

        FloatText effect = floating[nextFloat];
        if (!effect.Label.gameObject.activeSelf) liveFloats++;
        float x = 157 + nextFloat % 5 * 24;
        float y = stage.rect.height * 0.52f + nextFloat % 4 * 15;
        effect.Origin = new Vector2(x, -y);
        effect.Age = 0;
        effect.Label.rectTransform.anchoredPosition = effect.Origin;
        effect.Label.canvasRenderer.SetAlpha(1);
        Set(effect.Label, lastTapText);
        effect.Label.gameObject.SetActive(true);
        nextFloat = (nextFloat + 1) % floating.Length;

        tapAge = 0;
        coinArt.localScale = Vector3.one * 0.91f;
        SetFrame(burstFrames, ref burstFrame, 0);
        SetFrame(sparkleFrames, ref sparkleFrame, 0);
    }

    void Update()
    {
        if (coinArt == null || (tapAge >= 0.4f && liveFloats == 0)) return;
        float dt = Time.unscaledDeltaTime;
        if (tapAge < 0.4f)
        {
            tapAge += dt;
            float punch = tapAge < 0.085f ? Mathf.Lerp(0.91f, 1.065f, tapAge / 0.085f)
                : Mathf.Lerp(1.065f, 1, (tapAge - 0.085f) / 0.13f);
            coinArt.localScale = Vector3.one * punch;
            int frame = (int)(tapAge * EffectFps);
            SetFrame(burstFrames, ref burstFrame, frame);
            SetFrame(sparkleFrames, ref sparkleFrame, frame);
        }

        for (int i = 0; i < floating.Length && liveFloats > 0; i++)
        {
            FloatText effect = floating[i];
            if (!effect.Label.gameObject.activeSelf) continue;
            effect.Age += dt;
            float progress = effect.Age / FloatDuration;
            if (progress >= 1)
            {
                effect.Label.gameObject.SetActive(false);
                liveFloats--;
                continue;
            }
            effect.Label.rectTransform.anchoredPosition = effect.Origin + Vector2.up * (progress * 189);
            effect.Label.canvasRenderer.SetAlpha(1 - Mathf.SmoothStep(0, 1, progress));
        }
    }

    static void SetFrame(Image[] frames, ref int current, int next)
    {
        if (next >= frames.Length) next = -1;
        if (current == next) return;
        if (current >= 0) frames[current].enabled = false;
        current = next;
        if (current >= 0) frames[current].enabled = true;
    }
}
