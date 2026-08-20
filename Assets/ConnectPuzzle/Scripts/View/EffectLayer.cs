using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ConnectPuzzle.View
{
    /// <summary>
    /// Hiệu ứng: vụn bay, chữ điểm bay lên, loé sáng, rung bàn.
    /// Dùng pool để không cấp phát GameObject mỗi lần ăn ô.
    /// </summary>
    public sealed class EffectLayer
    {
        private readonly MonoBehaviour runner;
        private readonly RectTransform root;
        private readonly List<Image> particlePool = new List<Image>();
        private readonly List<Text> textPool = new List<Text>();
        private Image flash;

        /// <summary>
        /// Lấy lớp hiệu ứng có sẵn (UI từ prefab) hoặc tạo mới.
        ///
        /// KHÔNG gọi SetAsLastSibling khi nhận lại: lúc dựng bằng code lớp này ra đời giữa
        /// chừng rồi còn nhiều node sinh sau nó, nên vị trí thật của nó KHÔNG phải cuối.
        /// Đẩy xuống cuối là đổi thứ tự vẽ so với bản đang chạy.
        /// </summary>
        public EffectLayer(MonoBehaviour runner, RectTransform parent)
        {
            this.runner = runner;

            RectTransform existing = Ui.Reuse("Effects", parent);
            if (existing != null) { this.root = existing; return; }

            this.root = Ui.Node("Effects", parent);
            Ui.Stretch(this.root, 0, 0, 0, 0);
            this.root.SetAsLastSibling();
        }

        public void AttachFlash(RectTransform target)
        {
            RectTransform existing = Ui.Reuse("Flash", target);
            if (existing != null) { this.flash = existing.GetComponent<Image>(); return; }

            this.flash = Ui.Image("Flash", target, new Color(1, 1, 1, 0), PuzzleSprites.Square);
            Ui.Stretch(this.flash.rectTransform, 0, 0, 0, 0);
            this.flash.rectTransform.SetAsLastSibling();
        }

        private Image TakeParticle()
        {
            foreach (Image image in this.particlePool)
                if (!image.enabled) { image.enabled = true; return image; }

            Image created = Ui.Image("Particle" + this.particlePool.Count, this.root, Color.white, PuzzleSprites.Circle);
            created.rectTransform.anchorMin = created.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            this.particlePool.Add(created);
            return created;
        }

        private Text TakeText()
        {
            foreach (Text text in this.textPool)
                if (!text.enabled) { text.enabled = true; return text; }

            Text created = Ui.Text("Float" + this.textPool.Count, this.root, "", 44, Color.white,
                TextAnchor.MiddleCenter, FontStyle.Bold);
            created.rectTransform.anchorMin = created.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            created.rectTransform.sizeDelta = new Vector2(600, 90);
            this.textPool.Add(created);
            return created;
        }

        /// <summary>Vụn bay ra khi phá ô. position tính trong hệ local của lớp hiệu ứng.</summary>
        public void Burst(Vector2 position, Color color, int count, float spread)
        {
            for (int i = 0; i < count; i++)
            {
                Image particle = TakeParticle();
                float size = Random.Range(spread * 0.09f, spread * 0.2f);
                particle.rectTransform.sizeDelta = new Vector2(size, size);
                particle.rectTransform.anchoredPosition = position;
                particle.color = color;

                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(spread * 0.3f, spread * 1.1f);
                Vector2 target = position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) - 0.4f) * distance;
                this.runner.StartCoroutine(MoveParticle(particle, position, target, Random.Range(0.34f, 0.62f)));
            }
        }

        private IEnumerator MoveParticle(Image particle, Vector2 from, Vector2 to, float duration)
        {
            float elapsed = 0f;
            Color color = particle.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 2.2f);
                particle.rectTransform.anchoredPosition = Vector2.Lerp(from, to, eased);
                particle.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, 0.15f, t);
                particle.color = new Color(color.r, color.g, color.b, 1f - t);
                yield return null;
            }
            particle.enabled = false;
            particle.rectTransform.localScale = Vector3.one;
        }

        /// <summary>Chữ bay lên rồi tan — dùng cho +điểm, lời khen, số thứ tự nhóm.</summary>
        public void FloatText(Vector2 position, string content, Color color, int fontSize, float rise, float delay = 0f)
        {
            Text text = TakeText();
            text.text = content;
            text.color = color;
            text.fontSize = fontSize;
            text.rectTransform.anchoredPosition = position;
            this.runner.StartCoroutine(RiseText(text, position, rise, delay));
        }

        private IEnumerator RiseText(Text text, Vector2 from, float rise, float delay)
        {
            text.color = new Color(text.color.r, text.color.g, text.color.b, 0f);
            if (delay > 0f) yield return new WaitForSeconds(delay);

            const float duration = 1f;
            float elapsed = 0f;
            Color color = text.color;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float alpha = t < 0.18f ? t / 0.18f : (t > 0.7f ? 1f - (t - 0.7f) / 0.3f : 1f);
                float scale = t < 0.18f ? Mathf.Lerp(0.5f, 1.12f, t / 0.18f) : Mathf.Lerp(1.12f, 1f, (t - 0.18f) / 0.82f);
                text.rectTransform.anchoredPosition = from + new Vector2(0, rise * (1f - Mathf.Pow(1f - t, 2f)));
                text.rectTransform.localScale = Vector3.one * scale;
                text.color = new Color(color.r, color.g, color.b, alpha);
                yield return null;
            }
            text.enabled = false;
            text.rectTransform.localScale = Vector3.one;
        }

        public void Flash(float strength)
        {
            if (this.flash == null) return;
            this.runner.StartCoroutine(FlashRoutine(strength));
        }

        private IEnumerator FlashRoutine(float strength)
        {
            const float duration = 0.22f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float alpha = t < 0.4f ? Mathf.Lerp(0f, strength, t / 0.4f) : Mathf.Lerp(strength, 0f, (t - 0.4f) / 0.6f);
                this.flash.color = new Color(1, 1, 1, alpha);
                yield return null;
            }
            this.flash.color = new Color(1, 1, 1, 0);
        }

        public void Shake(RectTransform target, float magnitude)
        {
            this.runner.StartCoroutine(ShakeRoutine(target, magnitude));
        }

        private IEnumerator ShakeRoutine(RectTransform target, float magnitude)
        {
            Vector2 origin = target.anchoredPosition;
            const float duration = 0.34f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float decay = 1f - elapsed / duration;
                target.anchoredPosition = origin + new Vector2(
                    Random.Range(-magnitude, magnitude) * decay,
                    Random.Range(-magnitude, magnitude) * decay);
                yield return null;
            }
            target.anchoredPosition = origin;
        }

        public void Confetti(Vector2 center, float spread)
        {
            for (int i = 0; i < 60; i++)
            {
                Image particle = TakeParticle();
                float size = Random.Range(spread * 0.03f, spread * 0.06f);
                particle.rectTransform.sizeDelta = new Vector2(size, size * 1.6f);
                Vector2 from = center + new Vector2(Random.Range(-spread, spread), Random.Range(0f, spread * 0.3f));
                particle.rectTransform.anchoredPosition = from;
                particle.color = PuzzlePalette.Colors[Random.Range(0, PuzzlePalette.Colors.Length)];
                Vector2 to = from + new Vector2(Random.Range(-spread * 0.5f, spread * 0.5f), -spread * Random.Range(1f, 2f));
                this.runner.StartCoroutine(MoveParticle(particle, from, to, Random.Range(0.9f, 1.7f)));
            }
        }
    }
}
