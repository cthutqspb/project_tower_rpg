using UnityEngine;
using Unity.Entities;
using ProjectTowerRpg.ECS.Components;
using ProjectTowerRpg.Core.Localization;
using ProjectTowerRpg.Core.Items;
using ProjectTowerRpg.Core.Units; // ← Добавь юзинг базы юнитов
//using ProjectTowerRpg.Core.Factions;
using ProjectTowerRpg.Core.Abilities;
using ProjectTowerRpg.Core.Auras;

public class GameInitializer : MonoBehaviour
{
    void Awake()
    {
        // 1. Загружаем русский дефолт (Твой merge_tables на C#)
        LocalizationManager.LoadLanguage("Ru");
        
        // 2. Накатываем базу данных шмоток из JSON
        ItemsDatabase.Load();
        LootTables.Load();
        AbilitiesDatabase.Load();
        AurasDatabase.Load();
        FactionsDatabase.Load();

        // 🌟 3. НАКАТЫВАЕМ БАЗУ ДАННЫХ СУЩЕСТВ (Исправляет слепоту UI на чистой сцене!)
        if (!UnitsDatabase.IsLoaded)
        {
            UnitsDatabase.Load();
        }

        // =========================================================================
        // 🦾 ШАГ 4: РОЖДЕНИЕ ЦЕНТРАЛЬНОЙ ШИНЫ СОБЫТИЙ (Канон Presentation Buffer)
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
        }

        // =========================================================================
        // 🎰 ДEБAГ-ТEСТ: Проверяем, как бэкэнд видит наш Кристальный Меч в ОЗУ!
        // =========================================================================
        ItemConfig sword = ItemsDatabase.GetItem("crystal_sword");
        
        if (sword != null)
        {
            string realName = LocalizationManager.Get(sword.identity.name_key);
            string realDesc = LocalizationManager.Get(sword.identity.desc_key);

            Debug.Log($"🚀 [БЭКЭНД ТЕСТ]: Успешно вытащили паспорт предмета из ОЗУ!");
            Debug.Log($"⚔️ Предмет: {realName} ({sword.id}) | Качество: {sword.identity.quality} | Цена: {sword.identity.price}g");
            Debug.Log($"📜 Описание: {realDesc}");
            Debug.Log($"💪 Бонус к Силе: {sword.combat_stats.attributes.strength} | Урон: {sword.combat_stats.damage.min}-{sword.combat_stats.damage.max}");
        }
    }
}

