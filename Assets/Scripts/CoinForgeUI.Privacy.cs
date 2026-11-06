using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// IC-04 C3: "Privacy settings" button at the bottom of How to Play, in the existing secondary style.
/// Shown only when UMP reports PrivacyOptionsRequirementStatus.Required (e.g. EEA/UK users).
/// </summary>
public sealed partial class CoinForgeUI
{
    const float PrivacyWidth = 440;
    const int PrivacyFontSize = 36;
    Button helpPrivacy;
    bool privacyShown;

    void BuildPrivacy()
    {
        helpPrivacy = ActionButton(helpCard, "Privacy settings", null, OpenPrivacyOptions);
        ButtonContent(Label(helpPrivacy.transform, "Privacy settings", PrivacyFontSize, Ivory).rectTransform, 16, 16);
        helpPrivacy.gameObject.SetActive(false);
        AdManager.PrivacyOptionsChanged += OnPrivacyOptionsChanged;
    }

    bool PrivacyVisible => AdManager.I != null && AdManager.I.PrivacyOptionsRequired;

    /// <summary>Extra height the Help card bottom needs for the privacy row (0 when hidden).</summary>
    float PrivacyRowHeight => privacyShown ? targetSize + 16 : 0;

    void OnPrivacyOptionsChanged()
    {
        if (helpPrivacy == null || privacyShown == PrivacyVisible) return;
        privacyShown = PrivacyVisible;
        helpPrivacy.gameObject.SetActive(privacyShown);
        LayoutHelp(design.sizeDelta.y);
    }

    void LayoutPrivacy(float cardHeight)
    {
        if (!privacyShown) return;
        Place((RectTransform)helpPrivacy.transform, (920 - PrivacyWidth) * 0.5f, cardHeight - targetSize - 36, PrivacyWidth, targetSize);
    }

    void OpenPrivacyOptions()
    {
        if (adInFlight || AdManager.I == null) return;
        AdManager.I.ShowPrivacyOptions();
    }

    void ReleasePrivacy() => AdManager.PrivacyOptionsChanged -= OnPrivacyOptionsChanged;
}
