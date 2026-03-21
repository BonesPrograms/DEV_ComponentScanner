using XRL.World;
using System;
using System.Collections.Generic;
using System.Linq;
using ObjectInformation;
using XRL.UI;

namespace BeastScanner
{

    internal abstract class BaseReflective
    {

        protected static Type GetLimit(Type type)
        {
            return type == typeof(Effect) ? typeof(Effect) : typeof(IPart);
        }

        protected static bool CheckForNull(string value, SimpleToken token)
        {
            bool isNull = value.Equals("null", StringComparison.OrdinalIgnoreCase);
            if (token != SimpleToken.String && isNull)
                Popup.Show($"Cannot assign null to type {token}");
            return isNull;
        }


        protected static bool ValidToken(SimpleToken token, string txt)
        {
            if (token == SimpleToken.Unsupported)
            {
                IComponent<GameObject>.AddPlayerMessage($"{txt}.\nSupports: Int64, Int32, Bool, string.");
            }
            return token != SimpleToken.Unsupported;
        }

        protected static SimpleToken GetSimpleToken(Type type)
        {
            if (type == typeof(string))
                return SimpleToken.String;
            if (type == typeof(bool))
                return SimpleToken.Boolean;
            if (type == typeof(int))
                return SimpleToken.Int32;
            if (type == typeof(long))
                return SimpleToken.Int64;
            return SimpleToken.Unsupported;

        }

        protected static bool PickTarget(GameObject obj, string text, out GameObject pick)
        {
            IPart part = new() { ParentObject = obj };
            Cell cell = part.PickDestinationCell(80, AllowVis.OnlyVisible, Locked: true, IgnoreSolid: true, IgnoreLOS: true, RequireCombat: true, XRL.UI.PickTarget.PickStyle.EmptyCell, text, Snap: true);
            pick = cell?.GetCombatTarget(obj, true, true, true);
            bool value = pick != null;
            if (!value && cell != null)
                XRL.UI.Popup.ShowFail(cell.HasCombatObject() ? $"There is no one there you can {text}." : $"There is no one there to {text}");
            return value;
        }
        protected static T FindObject<T>(IList<T> list, string typeName)
        {
            return list.FirstOrDefault(x => x.GetType().Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
        }
    }
}