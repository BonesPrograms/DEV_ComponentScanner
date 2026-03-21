using XRL.Wish;
using XRL.World;
using System;
using XRL;
using System.Linq;
using System.Reflection;
using ObjectInformation;

namespace BeastScanner
{
     [HasWishCommand]

    internal class SetFieldCommand : BaseReflective
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
                FindField(typeName, fieldName, value, pick);
            }
        }

        static void FindField(string typeName, string fieldName, string value, GameObject pick)
        {
            object instance = FindObject(pick.PartsList, typeName);
            instance ??= FindObject(pick.Effects, typeName);
            if (instance == null)
            {
                IComponent<GameObject>.AddPlayerMessage($"{pick.DisplayName} ID: {pick.ID} does not have an IPart or Effect named {typeName}");
                return;
            }
            if (LoopForField(fieldName, instance, GetLimit(instance.GetType()), out var field))
            {
                SimpleToken token = GetSimpleToken(field.FieldType);
                if (ValidToken(token, $"{field.FieldType.Name} {field.Name} is not a valid type for setfield wish."))
                {
                    if (ProcessInputVaue(token, value, field, instance))
                        IComponent<GameObject>.AddPlayerMessage($" {field.FieldType.Name} {field.Name} set to value {value} on {instance.GetType().Name} in {pick.DisplayName} ID : {pick.ID}");
                }
            }
            else
                IComponent<GameObject>.AddPlayerMessage($"{instance.GetType().Name} does not have a field named {fieldName}");
        }

        static bool ProcessInputVaue(SimpleToken token, string value, FieldInfo field, object instance)
        {
            if (token == SimpleToken.String)
            {
                if (CheckForNull(value, token))
                    field.SetValue(instance, null);
                else
                    field.SetValue(instance, value);
                return true;
            }
            if (token == SimpleToken.Boolean && !CheckForNull(value, token))
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
            if (token == SimpleToken.Int32 && !CheckForNull(value, token))
            {
                if (int.TryParse(value, out int num))
                {
                    field.SetValue(instance, num);
                    return true;
                }
                else
                    IComponent<GameObject>.AddPlayerMessage($"Could not parse {value} for {field.FieldType.Name} {field.Name}");
            }
            if (token == SimpleToken.Int64 && !CheckForNull(value, token))
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

        static bool LoopForField(string fieldName, object instance, Type limit, out FieldInfo field)
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

}