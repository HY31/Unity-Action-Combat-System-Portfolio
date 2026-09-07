using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

// Exercises inactive copies only. Does not enter Play mode or modify battle state/assets.
public static class PlayerPartyHudAssemblyValidation
{
    private const string PrefabPath = "Assets/Prefabs/UI/ZZZHudV2/ZZZ_PlayerPartyHUD_Assembly.prefab";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/ZZZ HUD/Validate Player Party HUD Wiring")]
    public static void Run()
    {
        var report = new StringBuilder();
        var temporaryAssets = new List<UnityEngine.Object>();
        var host = new GameObject("HUD validation (inactive)");
        host.hideFlags = HideFlags.HideAndDontSave;
        host.SetActive(false);
        int assertions = 0;
        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException(message);
        }
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var copy = UnityEngine.Object.Instantiate(prefab, host.transform);
            var hud = copy.GetComponent<PlayerPartyHudAssemblyPresenter>();
            var active = Read<PlayerPartyHudAssemblyPresenter.SlotView>(hud, "activeSlot");
            var reserves = Read<PlayerPartyHudAssemblyPresenter.SlotView[]>(hud, "reserveSlots");
            var portraits = Read<Sprite[]>(hud, "memberPortraits");
            var partyObject = new GameObject("Test party");
            partyObject.transform.SetParent(host.transform, false);
            var party = partyObject.AddComponent<PartyManager>();
            var members = new PlayerController[3];
            string[] dataPaths = {
                "Assets/Characters/Ellen_Joe/Data/Character_Data_Ellen_Joe.asset",
                "Assets/Characters/Jane_Doe/Data/Character_Data_Jane_Doe.asset",
                "Assets/Characters/Corin/Data/Character_Data_Corin.asset"
            };
            float[] testRequirements = {20f, 60f, 80f};
            for (int i = 0; i < 3; i++)
            {
                var go = new GameObject("Test member " + i);
                go.transform.SetParent(host.transform, false);
                members[i] = go.AddComponent<PlayerController>();
                var source = AssetDatabase.LoadAssetAtPath<CharacterData>(dataPaths[i]);
                Check(source != null && source.enhancedSkillBranch != null, "Character data missing: " + dataPaths[i]);
                var data = UnityEngine.Object.Instantiate(source);
                var skill = UnityEngine.Object.Instantiate(source.enhancedSkillBranch);
                temporaryAssets.Add(data);
                temporaryAssets.Add(skill);
                report.AppendLine(source.name + ": actual entry=" + skill.requiredEntryEnergy + ", cost=" + skill.energyCost);
                skill.requiredEntryEnergy = testRequirements[i];
                skill.energyCost = testRequirements[i] - 5f;
                data.enhancedSkillBranch = skill;
                Set(members[i], "characterData", data);
                Set(members[i], "maxEnergy", 100f);
                Set(members[i], "currentEnergy", 10f);
                Set(members[i], "currentHp", members[i].CurrentMaxHp);
            }
            party.partyMembers = members;
            hud.Bind(party);
            var allSlots = new[] { active, reserves[0], reserves[1] };
            for (int index = 0; index < 3; index++)
            {
                Set(party, "currentIndex", index);
                hud.RefreshNow();
                int[] order = { index, (index + 1) % 3, (index + 2) % 3 };
                for (int slotIndex = 0; slotIndex < 3; slotIndex++)
                {
                    var slot = allSlots[slotIndex];
                    Check(slot.portrait.sprite == portraits[order[slotIndex]], "Portrait order incorrect.");
                    Check(Mathf.Abs(slot.healthFill.fillAmount - 1f) < 0.001f, "HP binding incorrect.");
                    Check(Mathf.Abs(slot.energyFill.fillAmount - 0.1f) < 0.001f, "Energy binding incorrect.");
                    CheckMarker(slot, testRequirements[order[slotIndex]] / 100f, Check);
                }
            }
            report.AppendLine("PASS: active/reserve reorder, portraits, health, energy and per-character thresholds.");

            var decibelObject = new GameObject("PTS binding test", typeof(RectTransform), typeof(TMPro.TextMeshProUGUI));
            decibelObject.hideFlags = HideFlags.HideAndDontSave;
            temporaryAssets.Add(decibelObject);
            var decibelView = decibelObject.AddComponent<DecibelHudText>();
            var decibelLabel = decibelObject.GetComponent<TMPro.TMP_Text>();
            hud.ConfigureDecibelHud(decibelView);
            Set(party, "currentIndex", 0);
            Set(members[0], "currentDecibel", 1234.9f);
            hud.RefreshNow();
            Check(decibelView.DisplayedValue == 1234 && decibelLabel.text.StartsWith("1234") &&
                  decibelLabel.text.Contains("PTS"), "Active character's decibel/PTS not updated.");
            Set(members[1], "currentDecibel", 3000f);
            Set(party, "currentIndex", 1);
            hud.RefreshNow();
            Check(decibelView.DisplayedValue == 3000 && decibelLabel.text.StartsWith("3000"),
                  "PTS does not follow character switch/ultimate-ready value.");
            Set(members[1], "currentDecibel", 0f);
            hud.RefreshNow();
            Check(decibelView.DisplayedValue == 0 && decibelLabel.text.StartsWith("0000"),
                  "PTS does not reset after ultimate spending.");
            foreach (int value in new[] {0, 1000, 2000, 3000})
            {
                Set(members[1], "currentDecibel", (float)value);
                hud.RefreshNow();
                Color32 left, right;
                DecibelHudText.ResolveColors(value, out left, out right);
                Check(Read<Color32>(decibelView, "leftColor").Equals(left) &&
                      Read<Color32>(decibelView, "rightColor").Equals(right), "PTS color stage incorrect.");
            }
            report.AppendLine("PASS: restored PTS number, character switch, 3000-ready, spending/reset, four color stages.");

            foreach (var slot in allSlots)
            {
                var gauge = slot.energyFill.rectTransform;
                var marker = slot.energyThresholdMarker;
                var size = marker.sizeDelta;
                var scale = marker.localScale;
                gauge.sizeDelta = new Vector2(537f, 39f);
                gauge.localScale = new Vector3(1.6f, 1.35f, 1f);
                gauge.anchoredPosition += new Vector2(33f, -8f);
                marker.parent.localScale = new Vector3(0.75f, 1.4f, 1f);
                foreach (float threshold in new[] {0f, 0.2f, 0.4f, 0.65f, 1f})
                {
                    PlayerPartyHudAssemblyPresenter.PositionEnergyThreshold(slot, threshold);
                    CheckMarker(slot, threshold, Check);
                    Check(marker.sizeDelta == size && marker.localScale == scale, "Authored marker size/scale changed.");
                }
            }
            report.AppendLine("PASS: resized/moved/scaled gauges and separately scaled marker parents; marker size preserved.");

            Set(party, "currentIndex", 0);
            var member = members[0];
            Set(member, "currentHp", member.CurrentMaxHp);
            Set(member, "currentEnergy", 19f);
            hud.RefreshNow();
            Check(active.energyFill.color == Read<Color>(hud, "energyNormalColor"), "Non-ready energy color incorrect.");
            Set(member, "currentEnergy", 20f);
            hud.RefreshNow();
            Check(active.energyThresholdMarker.GetComponent<Image>().color == Read<Color>(hud, "energyMarkerReadyColor"), "Threshold crossing does not turn marker red.");
            Set(member, "currentEnergy", 5f);
            hud.RefreshNow();
            Check(active.energyThresholdMarker.GetComponent<Image>().color == Read<Color>(hud, "energyMarkerNormalColor"), "Energy spend does not restore marker color.");
            Set(member, "currentHp", member.CurrentMaxHp * 0.4f);
            hud.RefreshNow();
            Check(Mathf.Abs(active.healthFill.fillAmount - 0.4f) < 0.001f, "HP loss not reflected.");
            Check(active.healthTrail.fillAmount > active.healthFill.fillAmount, "Red damage trail missing.");
            Check(active.healthTrail.sprite == active.healthFill.sprite, "Trail shape differs from HP fill.");
            Check(active.healthTrail.rectTransform.localScale == active.healthFill.rectTransform.localScale &&
                active.healthTrail.rectTransform.anchoredPosition3D == active.healthFill.rectTransform.anchoredPosition3D,
                "Trail does not follow authored HP geometry.");
            Check(active.healthText.text == $"{Mathf.RoundToInt(member.CurrentHp)} / {Mathf.RoundToInt(member.CurrentMaxHp)}", "HP text mismatch.");

            Set(members[1], "currentDecibel", 2999f);
            hud.RefreshNow();
            Check(!reserves[0].ultimateReadyIndicator.gameObject.activeSelf, "Ultimate ready below 3000.");
            Set(members[1], "currentDecibel", 3000f);
            hud.RefreshNow();
            Check(reserves[0].ultimateReadyIndicator.gameObject.activeSelf, "Ultimate missing at 3000.");
            Set(members[1], "currentDecibel", 0f);
            hud.RefreshNow();
            Check(!reserves[0].ultimateReadyIndicator.gameObject.activeSelf, "Ultimate not cleared after spend.");
            report.AppendLine("PASS: energy threshold crossing/spend, HP text/damage trail, ultimate 2999/3000/spend.");

            var pause = copy.GetComponentInChildren<PlayerPartyPauseButton>(true);
            var background = copy.GetComponentInChildren<PlayerHudFrameBackground>(true);
            var frame = background.transform.parent.Find("FrameOverlay");
            Check(pause.transform.parent == background.transform.parent &&
                background.transform.GetSiblingIndex() < pause.transform.GetSiblingIndex() &&
                pause.transform.GetSiblingIndex() < frame.GetSiblingIndex(), "Pause layering incorrect.");
            Check(pause.GetComponent<Button>().targetGraphic == pause.GetComponent<Image>(), "Pause button target graphic disconnected.");
            Check(!frame.GetComponent<Image>().raycastTarget && !background.raycastTarget, "Frame/backdrop blocks pause clicks.");
            var image = pause.GetComponent<Image>();
            Check(image.type == Image.Type.Simple && image.material.shader.name == "UI/Player HUD Light Background", "Pause cutout settings incorrect.");
            report.AppendLine("PASS: pause layer, Button target graphic, unobstructed raycast settings (no live click performed).");
            report.AppendLine("TOTAL PASS: " + assertions + " assertions.");
        }
        catch (Exception exception)
        {
            report.AppendLine("FAIL: " + exception);
            throw;
        }
        finally
        {
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.codex-temp/pause-image-repair/wiring-validation.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output, report.ToString());
            UnityEngine.Object.DestroyImmediate(host);
            foreach (var asset in temporaryAssets) UnityEngine.Object.DestroyImmediate(asset);
        }
        Debug.Log("[PlayerPartyHudAssemblyValidation] " + report);
    }

    private static void CheckMarker(PlayerPartyHudAssemblyPresenter.SlotView slot, float expected,
        Action<bool, string> check)
    {
        var gauge = slot.energyFill.rectTransform;
        Rect rect = slot.energyFill.GetPixelAdjustedRect();
        var sprite = slot.energyFill.sprite;
        Vector4 padding = DataUtility.GetPadding(sprite);
        float left = rect.xMin + rect.width * padding.x / sprite.rect.width;
        float right = rect.xMax - rect.width * padding.z / sprite.rect.width;
        Vector3 center = gauge.InverseTransformPoint(slot.energyThresholdMarker.TransformPoint(slot.energyThresholdMarker.rect.center));
        float measured = (center.x - left) / (right - left);
        check(Mathf.Abs(measured - expected) < 0.001f,
            "Marker position: expected=" + expected + ", measured=" + measured);
        check(Mathf.Abs(center.y - rect.center.y) < 0.05f, "Marker not centered on energy gauge.");
    }

    private static T Read<T>(object target, string field) => (T)target.GetType().GetField(field, Fields).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
}
