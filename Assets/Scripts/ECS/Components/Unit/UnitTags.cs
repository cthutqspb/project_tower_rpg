using Unity.Entities;

namespace ProjectTowerRpg.ECS.Components
{
    // 🧙‍♂️ МАРКЕР ИГРОКА: Для перехвата контроля инпутом и обновления HUD
    public struct PlayerTag : IComponentData {}

    // 💀 МАРКЕР МОНСТРА: Для агрессивного ИИ, агро-ренжей и ротации боевых спеллов
    public struct MonsterTag : IComponentData {}

    // 👥 МАРКЕР NPC: Для мирных торговцев, квестодателей и диалоговых окон
    public struct NpcTag : IComponentData {}
}

