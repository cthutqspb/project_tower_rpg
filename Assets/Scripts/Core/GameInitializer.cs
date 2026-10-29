using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Localization;
using ProjectTowerRpg.Core.Data;
using ProjectTowerRpg.Core.Presentation;
//using ProjectTowerRpg.Core.Factions;

using TMPro;

public class GameInitializer : MonoBehaviour
{
    [Header("Прогрев VFX аур")]
    [SerializeField] private TMP_FontAsset _auraFont;
    [SerializeField] private GameObject _auraVfxPrefab;
    [SerializeField] private string _auraGlyphs = "";//"󰞇󱐌󰸓󱨨��󱄯"; // запасной список, если из БД не собрать

    void Awake()
    {
        // 1. Загружаем русский дефолт (Твой merge_tables на C#)
        LocalizationManager.LoadLanguage("Ru");
        // 2. Базы
        GameDB.Initialize();
        
        // =========================================================================
        // 🦾 ШАГ 3: РОЖДЕНИЕ ЦЕНТРАЛЬНОЙ ШИНЫ СОБЫТИЙ (Канон Presentation Buffer)
        // =========================================================================
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            var em = world.EntityManager;

            // Создаем чистую синглтон-сущность для трансляции анимаций
            Entity eventBufferEntity = em.CreateEntity();
            
            // Навешиваем тег-паспорт, по которому Диспетчер найдет этот буфер
            em.AddComponentData(eventBufferEntity, new PresentationEventBufferTag());
            
            // Генерируем сам DynamicBuffer в ОЗУ симуляции
            em.AddBuffer<PresentationEvent>(eventBufferEntity);

            Debug.Log("🌐 [GameInitializer]: Синглтон шины PresentationEvent успешно развернут в ECS!");

            // 🌐 ШИНА ГЕЙМПЛЕЙНЫХ СОБЫТИЙ
            Entity gameEventsEntity = em.CreateEntity();
            em.AddComponentData(gameEventsEntity, new GameEvent());
            em.AddBuffer<EntityKilledEvent>(gameEventsEntity);
            em.AddBuffer<ItemUsedEvent>(gameEventsEntity);
            em.AddBuffer<DiscoverLocationEvent>(gameEventsEntity);
            em.AddBuffer<DialogueCompletedEvent>(gameEventsEntity);
            em.AddBuffer<QuestReadyToClaimEvent>(gameEventsEntity);

            Debug.Log("🌐 [GameInitializer]: Синглтон GameEvent с 4 буферами развернут в ECS!");
        }        

        // 4. Прогрев VFX аур — глифы СОБИРАЕМ из баз
        string glyphs = GameDB.GetAllIconChars();

        if (string.IsNullOrEmpty(glyphs))
        {
            Debug.LogWarning("[GameInitializer] GetAllIconChars вернул пусто — использую fallback.");
            glyphs = _auraGlyphs;  // fallback (можно оставить пустым)
        }
        else
        {
            Debug.Log($"[GameInitializer] Прогрев TMP: {glyphs.Length} глифов.");
        }

        StartCoroutine(AuraVfxPrewarm.Warmup(_auraFont, glyphs, _auraVfxPrefab, transform));
    }
}

