// using UnityEngine;
// using Unity.Entities;
// using ProjectTowerRpg.ECS.Components;
//
// namespace ProjectTowerRpg.ECS.Authoring
// {
//     // Зрячий, понятный скрипт для инспектора Unity
//     public class ProjectileDatabaseAuthoring : MonoBehaviour
//     {
//         [Header("Spells & Projectiles Catalog")]
//         public GameObject FrostboltPrefab; 
//     }
//
//     // Запекатель каталога снарядов в ECS мир
//     public class ProjectileDatabaseBaker : Baker<ProjectileDatabaseAuthoring>
//     {
//         public override void Bake(ProjectileDatabaseAuthoring authoring)
//         {
//             var entity = GetEntity(TransformUsageFlags.None);
//
//             // Переводим тяжелый GameObject куба в легкий Entity-образец снаряда
//             Entity ecsPrefab = GetEntity(authoring.FrostboltPrefab, TransformUsageFlags.Dynamic);
//
//             // Намертво запекаем базу данных снарядов
//             AddComponent(entity, new ProjectileDatabase
//             {
//                 FrostboltPrefab = ecsPrefab
//             });
//         }
//     }
// }
//
