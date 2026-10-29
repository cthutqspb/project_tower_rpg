using System.Collections;
using TMPro;
using UnityEngine;

namespace ProjectTowerRpg.Core.Presentation
{
    // Прогрев TMP-атласа, шейдерных вариантов и инстанса материала
    // для аурных глифов. Вызывать один раз на старте сцены.
    public static class AuraVfxPrewarm
    {
        public static IEnumerator Warmup(TMP_FontAsset font, string allGlyphs, GameObject auraPrefab, Transform parent)
        {
            yield return null;
            // 1) Растеризуем все нужные глифы в атлас заранее
            if (font != null && !string.IsNullOrEmpty(allGlyphs))
            {
                font.TryAddCharacters(allGlyphs, out string missing);
                if (!string.IsNullOrEmpty(missing))
                    Debug.LogWarning($"[AuraVfxPrewarm] Глифы не найдены в шрифте: {missing}");
            }

            // 2) Инстанцируем префаб за кадром на один кадр:
            //    компилятся варианты шейдера, создаётся инстанс материала через fontMaterial
            if (auraPrefab != null)
            {
                var dummy = Object.Instantiate(auraPrefab, parent);
                dummy.transform.position = new Vector3(0f, -10000f, 0f);

                var tmp = dummy.GetComponentInChildren<TextMeshPro>();
                if (tmp != null)
                {
                    tmp.text = allGlyphs;
                    _ = tmp.fontMaterial; // принудительно создаём инстанс материала
                    tmp.ForceMeshUpdate();
                }

                // Даём отрендериться хотя бы один кадр, потом убираем
                yield return null;
                Object.Destroy(dummy);
            }
        }
    }
}
