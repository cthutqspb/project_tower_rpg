using UnityEngine;
using ProjectTowerRpg.Core.Localization;
using ProjectTowerRpg.Core.Items;

public class GameInitializer : MonoBehaviour
{
    void Awake()
    {
        // 1. Загружаем русский дефолт (Твой merge_tables на C#)
        LocalizationManager.LoadLanguage("Ru");
        
        // 2. Накатываем базу данных шмоток из JSON
        ItemsDatabase.Load();

        // =========================================================================
        // 🎰 ДEБAГ-ТEСТ: Проверяем, как бэкэнд видит наш Кристальный Меч в ОЗУ!
        // =========================================================================
        ItemConfig sword = ItemsDatabase.GetItem("crystal_sword");
        
        if (sword != null)
        {
            // Вытаскиваем локализованное имя меча из нашего ItemsDatabase.json -> Locales/ru/items.json
            string realName = LocalizationManager.Get(sword.identity.name_key);
            string realDesc = LocalizationManager.Get(sword.identity.desc_key);

            Debug.Log($"🚀 [БЭКЭНД ТЕСТ]: Успешно вытащили паспорт предмета из ОЗУ!");
            Debug.Log($"⚔️ Предмет: {realName} ({sword.id}) | Качество: {sword.identity.quality} | Цена: {sword.identity.price}g");
            Debug.Log($"📜 Описание: {realDesc}");
            Debug.Log($"💪 Бонус к Силе: {sword.combat_stats.attributes.strength} | Урон: {sword.combat_stats.damage.min}-{sword.combat_stats.damage.max}");
        }
    }
}

