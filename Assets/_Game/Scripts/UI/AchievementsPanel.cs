using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KartCup
{
    /// The achievements as a list you can read on a phone (12 Oct 2026, the user: "there must be a good amount of lists,
    /// so it should be good UX wise"). Two tabs: TO DO and DONE. TO DO puts what can be claimed on top (lit gold, a CLAIM
    /// button, CLAIM ALL when there are several), then whatever is nearest to done, then the rest; DONE keeps what has
    /// been claimed. Two columns of big cards in a scrolling list: the title, what to do, a bar with its count, and the
    /// chest and stars it pays. Built at runtime from one row the menu builder made (MenuSetup.Achievements).
    public sealed class AchievementsPanel : MonoBehaviour
    {
        [SerializeField] TMP_Text total;
        [SerializeField] Image totalFill;
        [SerializeField] ScrollRect scroll;
        [SerializeField] RectTransform content;
        [SerializeField] GameObject rowTemplate;
        [SerializeField] Image[] tabBacks = System.Array.Empty<Image>();
        [SerializeField] TMP_Text[] tabLabels = System.Array.Empty<TMP_Text>();
        [SerializeField] GameObject claimAll;
        [SerializeField] TMP_Text claimAllLabel, empty;
        [SerializeField] Sprite[] chests = System.Array.Empty<Sprite>();          // closed, wood to golden
        [SerializeField] Color gold = new(1f, 0.694f, 0.149f), muted = new(0.706f, 0.655f, 0.871f), tabOn = new(0.29f, 0.145f, 0.69f), tabOff = new(0.11f, 0.08f, 0.24f);
        [SerializeField] float rowW = 1000f, rowH = 172f, gapX = 32f, gapY = 22f;

        sealed class Row
        {
            public Achievements.Def def; public RectTransform rt;
            public Image edge, icon, fill, chest; public TMP_Text title, line, count, stars; public Button claim; public GameObject done;
        }
        readonly List<Row> rows = new();
        int tab;          // 0 TO DO, 1 DONE

        void OnEnable() { Achievements.Changed += Show; tab = 0; Show(); Top(); }
        void OnDisable() => Achievements.Changed -= Show;
        public void ShowInEditor() { tab = 0; Show(); }
        public void ShowDoneInEditor() { tab = 1; Show(); }

        public void ToDo() { if (tab == 0) return; tab = 0; Show(); Top(); }
        public void Done() { if (tab == 1) return; tab = 1; Show(); Top(); }

        public void ClaimAll()
        {
            int n = 0;
            foreach (var d in Achievements.All.Where(Achievements.CanClaim).ToList()) if (Achievements.Claim(d)) n++;
            if (n == 0) return;
            Haptics.Play(Haptics.Kind.Win); AudioDirector.Instance?.Confirm();
        }

        void Claim(Row r)
        {
            if (!Achievements.Claim(r.def)) return;
            Haptics.Play(Haptics.Kind.Win); AudioDirector.Instance?.Confirm();
        }

        void Top() { if (scroll != null) scroll.verticalNormalizedPosition = 1f; }

        void Build()
        {
            if (rows.Count > 0 || rowTemplate == null || content == null) return;
            rowTemplate.SetActive(false);
            foreach (var d in Achievements.All)
            {
                var go = Instantiate(rowTemplate, content);
                go.name = "A_" + d.id;
                var t = go.transform;
                var r = new Row
                {
                    def = d, rt = (RectTransform)t,
                    edge = t.Find("Edge")?.GetComponent<Image>(), icon = t.Find("Icon")?.GetComponent<Image>(),
                    fill = t.Find("Bar/Fill")?.GetComponent<Image>(), chest = t.Find("Reward/Chest")?.GetComponent<Image>(),
                    title = t.Find("Title")?.GetComponent<TMP_Text>(), line = t.Find("Line")?.GetComponent<TMP_Text>(),
                    count = t.Find("Count")?.GetComponent<TMP_Text>(), stars = t.Find("Reward/Stars")?.GetComponent<TMP_Text>(),
                    claim = t.Find("Claim")?.GetComponent<Button>(), done = t.Find("Done")?.gameObject,
                };
                if (r.claim != null) { var row = r; r.claim.onClick.AddListener(() => Claim(row)); }
                rows.Add(r);
            }
        }

        void Show()
        {
            Build();
            var all = Achievements.All;
            int claimable = Achievements.ClaimableCount, doneCount = Achievements.DoneCount, claimed = all.Count(Achievements.IsClaimed);
            if (total != null) total.text = $"{doneCount} / {all.Length} DONE";
            if (totalFill != null) totalFill.fillAmount = all.Length > 0 ? doneCount / (float)all.Length : 0f;
            for (int i = 0; i < tabLabels.Length && i < 2; i++)
                if (tabLabels[i] != null) tabLabels[i].text = i == 0 ? $"TO DO  {all.Length - claimed}" : $"DONE  {claimed}";
            for (int i = 0; i < tabBacks.Length && i < 2; i++) if (tabBacks[i] != null) tabBacks[i].color = i == tab ? tabOn : tabOff;
            if (claimAll != null) claimAll.SetActive(tab == 0 && claimable >= 2);
            if (claimAllLabel != null) claimAllLabel.text = $"CLAIM ALL  ·  {claimable}";

            float Ratio(Achievements.Def d) => d.goal > 0 ? Achievements.Progress(d) / (float)d.goal : 0f;
            var shown = rows.Where(r => tab == 0 ? !Achievements.IsClaimed(r.def) : Achievements.IsClaimed(r.def))
                            .OrderByDescending(r => Achievements.CanClaim(r.def))           // ready to claim first
                            .ThenByDescending(r => Ratio(r.def))                            // then nearest to done
                            .ThenBy(r => r.def.goal).ToList();
            foreach (var r in rows) r.rt.gameObject.SetActive(false);
            for (int k = 0; k < shown.Count; k++)
            {
                var r = shown[k];
                r.rt.gameObject.SetActive(true);
                r.rt.anchoredPosition = new Vector2((k % 2) * (rowW + gapX), -(k / 2) * (rowH + gapY));
                Fill(r);
            }
            if (content != null) content.sizeDelta = new Vector2(content.sizeDelta.x, Mathf.Max(1, (shown.Count + 1) / 2) * (rowH + gapY));
            if (empty != null)
            {
                empty.gameObject.SetActive(shown.Count == 0);
                empty.text = tab == 0 ? "Every one done. Nothing left to prove." : "None claimed yet. Finish a race to start.";
            }
        }

        void Fill(Row r)
        {
            var d = r.def;
            bool ready = Achievements.CanClaim(d), claimed = Achievements.IsClaimed(d);
            int p = Achievements.Progress(d);
            if (r.title != null) r.title.text = d.title.ToUpperInvariant();
            if (r.line != null) r.line.text = d.line;
            if (r.count != null) { r.count.text = claimed || ready ? "DONE" : $"{p} / {d.goal}"; r.count.color = claimed || ready ? gold : Color.white; }
            if (r.fill != null) { r.fill.fillAmount = d.goal > 0 ? p / (float)d.goal : 0f; r.fill.color = ready || claimed ? gold : muted; }
            if (r.icon != null) r.icon.color = ready || claimed ? gold : new Color(muted.r, muted.g, muted.b, 0.5f);
            if (r.edge != null) r.edge.color = ready ? gold : claimed ? new Color(gold.r, gold.g, gold.b, 0.35f) : new Color(0.29f, 0.145f, 0.69f, 0.6f);
            if (r.chest != null && (int)d.reward < chests.Length) { r.chest.sprite = chests[(int)d.reward]; r.chest.color = claimed ? new Color(1f, 1f, 1f, 0.45f) : Color.white; }
            if (r.stars != null) r.stars.text = $"+{d.stars}";
            if (r.claim != null) r.claim.gameObject.SetActive(ready);
            if (r.done != null) r.done.SetActive(claimed);
        }
    }
}
