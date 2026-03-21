using XRL.Wish;
using XRL.World;
using System;
using System.Collections.Generic;
using XRL;
using System.Linq;
using System.Reflection;
using System.ComponentModel;
using ObjectInformation;
using XRL.UI;

namespace BeastScanner
{
    [HasWishCommand]
    internal static class SetFieldCommand
    {

        [WishCommand("setfield")] //next up- a really crappy method runner

        static void SetField(string input)
        {
            string[] strings = input.Split(":");
            if (strings.Length < 3)
                IComponent<GameObject>.AddPlayerMessage("Incomplete parameters. parameters: Type:Field:Value");
            else if (ScanCommand.PickTarget(The.Player, "setfield", out var pick))
            {
                string typeName = strings[0];
                string fieldName = strings[1];
                string value = strings[2];
                FindType(typeName, fieldName, value, pick);
            }
        }

        static T FindObject<T>(IList<T> list, string typeName)
        {
            return list.FirstOrDefault(x => x.GetType().Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
        }

        static void FindType(string typeName, string fieldName, string value, GameObject pick)
        {
            object instance = FindObject(pick.PartsList, typeName);
            instance ??= FindObject(pick.Effects, typeName);
            if (instance == null)
            {
                IComponent<GameObject>.AddPlayerMessage($"{pick.DisplayName} ID: {pick.ID} does not have an IPart or Effect named {typeName}");
                return;
            }
            if (FindField(fieldName, instance, GetLimit(instance), out var field))
            {
                SimpleToken token = GetSimpleToken(field.FieldType);
                if (ValidToken(token, field))
                {
                    if (ProcessInputVaue(token, value, field, instance))
                        IComponent<GameObject>.AddPlayerMessage($" {field.FieldType.Name} {field.Name} set to value {value} on {instance.GetType().Name} in {pick.DisplayName} ID : {pick.ID}");
                }
            }
            else
                IComponent<GameObject>.AddPlayerMessage($"{instance.GetType().Name} does not have a field named {fieldName}");
        }

        static bool CheckForNull(string value, FieldInfo field)
        {
            bool isNull = value.Equals("null", StringComparison.OrdinalIgnoreCase);
            if (field.FieldType != typeof(string) && isNull)
                IComponent<GameObject>.AddPlayerMessage($"Cannot assign null to {field.FieldType.Name} {field.Name}.");
            return isNull;
        }

        static bool ProcessInputVaue(SimpleToken token, string value, FieldInfo field, object instance)
        {
            if (token == SimpleToken.String)
            {
                if (CheckForNull(value, field))
                    field.SetValue(instance, null);
                else
                    field.SetValue(instance, value);
                return true;
            }
            if (token == SimpleToken.Boolean && !CheckForNull(value, field))
            {
                if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    field.SetValue(instance, false);
                    return true;
                }
                else if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    field.SetValue(instance, true);
                    return true;
                }
                else
                    IComponent<GameObject>.AddPlayerMessage($"Could not parse {value} for {field.FieldType.Name} {field.Name}");
            }
            if (token == SimpleToken.Int32 && !CheckForNull(value, field))
            {
                if (int.TryParse(value, out int num))
                {
                    field.SetValue(instance, num);
                    return true;
                }
                else
                    IComponent<GameObject>.AddPlayerMessage($"Could not parse {value} for {field.FieldType.Name} {field.Name}");
            }
            if (token == SimpleToken.Int64 && !CheckForNull(value, field))
            {
                if (long.TryParse(value, out long num))
                {
                    field.SetValue(instance, num);
                    return true;
                }
                else
                    IComponent<GameObject>.AddPlayerMessage($"Could not parse {value} for {field.FieldType} {field.Name}");
            }
            return false;
        }

        static bool ValidToken(SimpleToken token, FieldInfo field)
        {
            if (token == SimpleToken.Invalid)
            {
                IComponent<GameObject>.AddPlayerMessage($"{field.FieldType.Name} {field.Name} is not a valid type for setfield wish.\nSupports: Int64, Int32, Bool, string.");
            }
            return token != SimpleToken.Invalid;
        }

        static Type GetLimit(object instance)
        {
            return instance is Effect ? typeof(Effect) : typeof(IPart);
        }

        static SimpleToken GetSimpleToken(Type type)
        {
            if (type == typeof(string))
                return SimpleToken.String;
            if (type == typeof(bool))
                return SimpleToken.Boolean;
            if (type == typeof(int))
                return SimpleToken.Int32;
            if (type == typeof(long))
                return SimpleToken.Int64;
            return SimpleToken.Invalid;

        }
        static bool FindField(string fieldName, object instance, Type limit, out FieldInfo field)
        {
            Type type = instance.GetType();
            field = null;
            while (type != limit)
            {
                FieldInfo[] fields = type.GetFields(ComponentScanner.Flags);
                field = fields.FirstOrDefault(x => x.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                if (field != null)
                    break;
                type = type.BaseType;

            }
            return field != null;

        }
    }


    [HasWishCommand]
    internal static class ScanCommand
    {

        [WishCommand("readfx")]

        static void ScanFX(string name)
        {
            ScanWish(name, "read effect", x => x.Effects);
        }

        [WishCommand("readpart")]

        static void ScanPart(string name)
        {
            ScanWish(name, "read part", x => x.PartsList);
        }

        static bool CheckList<T>(IList<T> list, string name, out T component) where T : IComponent<GameObject>
        {
            component = list.FirstOrDefault(x => x.GetType().Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return component != null;
        }

        public static bool PickTarget(GameObject obj, string text, out GameObject pick)
        {
            IPart part = new() { ParentObject = obj };
            Cell cell = part.PickDestinationCell(80, AllowVis.OnlyVisible, Locked: true, IgnoreSolid: true, IgnoreLOS: true, RequireCombat: true, XRL.UI.PickTarget.PickStyle.EmptyCell, text, Snap: true);
            pick = cell?.GetCombatTarget(obj, true, true, true);
            bool value = pick != null;
            if (!value && cell != null)
                XRL.UI.Popup.ShowFail(cell.HasCombatObject() ? $"There is no one there you can {text}." : $"There is no one there to {text}");
            return value;
        }
        static void ScanWish<T>(string name, string action, Func<GameObject, IList<T>> expr) where T : IComponent<GameObject>
        {
            if (PickTarget(The.Player, action, out GameObject pick))
            {
                string baseClass = typeof(T).Name;
                if (CheckList(expr(pick), name, out T component))
                {
                    IComponent<GameObject>.AddPlayerMessage($"Beginning read of fields in {typeof(T).Name} {component.GetType().Name} on  {pick.DisplayName} {pick.ID}");
                    new ComponentScanner(typeof(T)).Scan(component);
                }
                else
                {
                    IComponent<GameObject>.AddPlayerMessage($"{pick.DisplayName} {pick.ID} does not have an {baseClass} named {name} in their {baseClass} list.");
                }
            }
        }
    }


}