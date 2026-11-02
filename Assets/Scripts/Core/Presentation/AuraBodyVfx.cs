using UnityEngine;
using System.Collections;

namespace ProjectTowerRpg.Core.Presentation
{
    public class AuraBodyVfx : MonoBehaviour
    {
        public string AuraId;
        public string VfxType;

        private Vector3 _targetScale;
        private Renderer _renderer;
        private Coroutine _spawnCoroutine;
        private Coroutine _rippleCoroutine;
        
        // 🎯 ID параметра в Shader Graph
        private static readonly int HitRippleId = Shader.PropertyToID("HitRipple");

        private void Awake()
        {
            // Ищем рендерер для управления шейдером
            _renderer = GetComponent<Renderer>();
            if (_renderer == null) _renderer = GetComponentInChildren<Renderer>();
        }

        /// <summary>
        /// 🎯 Вызывается из PresentationEventSystem сразу после спавна.
        /// Задает целевой масштаб, ставит объект в 0.1 и запускает анимацию появления.
        /// </summary>
        public void Setup(string auraId, string vfxType, Vector3 targetScale)
        {
            AuraId = auraId;
            VfxType = vfxType;
            _targetScale = targetScale;

            // Мгновенно ставим маленький масштаб перед анимацией
            transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            
            PlaySpawnAnimation();
        }

        /// <summary>
        /// Анимация появления: щит "вырастает" из точки до нормального размера.
        /// </summary>
        public void PlaySpawnAnimation()
        {
            if (_spawnCoroutine != null) StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = StartCoroutine(SpawnRoutine());
        }

        private IEnumerator SpawnRoutine()
        {
            float duration = 0.8f;
            float elapsed = 0f;

            Vector3 startScale = new Vector3(0.1f, 0.1f, 0.1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Ease Out Cubic для сочности
                float easedT = 1f - Mathf.Pow(1f - t, 3f); 
                
                transform.localScale = Vector3.Lerp(startScale, _targetScale, easedT);
                yield return null;
            }

            transform.localScale = _targetScale;
            _spawnCoroutine = null;
        }

        /// <summary>
        /// Анимация смерти: щит "схлопывается" в точку и удаляется.
        /// </summary>
        public void PlayDeathAnimation()
        {
            if (_spawnCoroutine != null) StopCoroutine(_spawnCoroutine);
            if (_rippleCoroutine != null) StopCoroutine(_rippleCoroutine);

            StartCoroutine(DeathRoutine());
        }

        private IEnumerator DeathRoutine()
        {
            float duration = 0.25f;
            float elapsed = 0f;
            Vector3 currentScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Ease In Cubic (сжимается всё быстрее)
                float easedT = t * t * t;
                transform.localScale = Vector3.Lerp(currentScale, Vector3.zero, easedT);
                
                yield return null;
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// Эффект "ряби" при ударе. Вызывается из PresentationEventSystem.
        /// </summary>
        public void TriggerHitRipple()
        {
            if (_rippleCoroutine != null) StopCoroutine(_rippleCoroutine);
            _rippleCoroutine = StartCoroutine(RippleRoutine());
        }

        private IEnumerator RippleRoutine()
        {
            if (_renderer == null) yield break;

            // 🎯 Создаём инстанс материала, чтобы изменения были только у этого щита
            Material mat = _renderer.material;
            
            float duration = 0.3f;
            float elapsed = 0f;

            // Мгновенно включаем рябь (1)
            mat.SetFloat(HitRippleId, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Плавно гасим от 1 до 0
                float rippleValue = 1f - t; 
                mat.SetFloat(HitRippleId, rippleValue);
                
                yield return null;
            }

            mat.SetFloat(HitRippleId, 0f);
            _rippleCoroutine = null;
        }

        /// <summary>
        /// Сброс при повторном касте (если щит уже висит).
        /// </summary>
        public void RefreshLifetime()
        {
            if (_rippleCoroutine != null)
            {
                StopCoroutine(_rippleCoroutine);
                _rippleCoroutine = null;
                
                if (_renderer != null)
                {
                    _renderer.material.SetFloat(HitRippleId, 0f);
                }
            }
        }
    }
}
