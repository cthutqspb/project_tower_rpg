using UnityEngine;
using TMPro;

namespace ProjectTowerRpg.Core.Shared
{
    public class AuraVfxLifetime : MonoBehaviour
    {
        private TextMeshPro _tmpText;
        private Material _instancedMaterial;

        private Color _faceColor;
        private Color _glowColor;

        private float _lifetime = 2.0f;
        private float _fadeDuration = 0.4f;
        private float _elapsedTime = 0f;

        private Vector3 _startLocalPos;
        private float _floatSpeed = 1.5f;
        private float _floatAmount = 0.15f;

                public void Initialize(TextMeshPro tmpText, Color faceColor, Color glowColor, float duration = 2.0f)
        {
            _tmpText = tmpText;
            _faceColor = faceColor;
            _glowColor = glowColor;
            _lifetime = duration;

            // 🦾 СИ-ФИКС №1: ТОТАЛЬНЫЙ СБРОС ТАЙМЕРА!
            // Обнуляем прошедшее время, чтобы Fade In заново зажёг руну в максимум,
            // а интерполятор альфы не улетал в покадровый ступор!
            _elapsedTime = 0f;

            if (_tmpText != null)
            {
                _instancedMaterial = _tmpText.fontMaterial;
            }

            // 🦾 СИ-ФИКС №2: ЗАЩИТА ГЕОМЕТРИИ ОТ ТЕЛЕПОРТАЦИИ ВЫШЕ ГОЛОВЫ!
            // Мы запоминаем локальную позицию над Meadows-скелетом строго ОДИН РАЗ —
            // в момент первой сборки префаба на сцене. При повторных фаерболах 
            // база покачивания остаётся на месте, убирая улёты глифа в космос!
            if (_startLocalPos == Vector3.zero)
            {
                _startLocalPos = transform.localPosition;
            }
        }

        private void Update()
        {
            if (_tmpText == null || _instancedMaterial == null) return;

            _elapsedTime += Time.deltaTime;

            // 📐 МЯГКОЕ ММО-ПОКАЧИВАНИЕ + СИ-ФИКС ДРОЖАНИЯ МАГИИ
            float alphaProgress = _elapsedTime / _lifetime;
            
            float jitterX = Mathf.Sin(Time.time * 30f) * 0.015f; 
            float jitterY = Mathf.Sin(Time.time * 35f) * 0.015f; 

            Vector3 currentOffset = new Vector3(
                jitterX, 
                Mathf.Sin(Time.time * _floatSpeed) * _floatAmount + (alphaProgress * 0.1f) + jitterY, 
                0f
            );
            transform.localPosition = _startLocalPos + currentOffset;

            // 🧬 АЛЬФА-КАНАЛ (FADE IN / FADE OUT)
            float currentAlpha = 1f;

            if (_elapsedTime < _fadeDuration)
            {
                currentAlpha = _elapsedTime / _fadeDuration;
            }
            else if (_elapsedTime > (_lifetime - _fadeDuration))
            {
                float remainingTime = _lifetime - _elapsedTime;
                currentAlpha = Mathf.Max(0f, remainingTime / _fadeDuration);
            }

            // FACE: Корректно крутим float-альфу прозрачности лица
            Color face = _faceColor;
            face.a = _faceColor.a * currentAlpha;
            _instancedMaterial.SetColor("_FaceColor", face);

            // GLOW: Передаем HDR-цвет неона
            _instancedMaterial.SetColor("_GlowColor", _glowColor); 

            // =========================================================================
            // 🦾 ФИКСАЦИЯ УЛЬТРА-ТОНКОГО НЕОНА КАНOН (Без раздувания от дистанции)
            // =========================================================================
            _instancedMaterial.SetFloat("_FaceDilate", 0.0f);      
            _instancedMaterial.SetFloat("_FaceSoftness", 0.0f);  
            
            // Сжимаем масштаб эффекта, превращая контур в тонкую нить
            _instancedMaterial.SetFloat("_ScaleRatioB", 0.35f); 
            _instancedMaterial.SetFloat("_GlowOffset", -0.05f); 
            _instancedMaterial.SetFloat("_GlowInner", -0.01f);  

            // Внешний радиус и жесткость затухают синхронно с альфой появления/смерти руны
            _instancedMaterial.SetFloat("_GlowOuter", 0.05f * currentAlpha);   
            _instancedMaterial.SetFloat("_GlowPower", 1.0f * currentAlpha);

            // 🪦 СМЕРТЬ РУНЫ
            if (_elapsedTime >= _lifetime)
            {
                Destroy(gameObject);
            }
        }
    }
}

