using System.Collections.Generic;
using ProjectTowerRpg.ECS.Actions;

namespace ProjectTowerRpg.Core.UI
{
    public class MenuAction
    {
        public string NameKey;
        public ActionKind Action;
        public Dictionary<string, object> Data;
    }
}
