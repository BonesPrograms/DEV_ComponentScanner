
using XRL.Wish;
using XRL.World;
using System;
using System.Collections.Generic;
using XRL;
using System.Linq;
using System.Reflection;
using ObjectInformation;
using XRL.UI;
using System.Text;

namespace BeastScanner
{
    [HasWishCommand]
    internal class InvokeCommand : BaseReflective
    {
        [WishCommand("invoke")]

        static void Invoke(string input)
        {
            string[] strings = input.Split(":");
            if (strings.Length < 2)
                IComponent<GameObject>.AddPlayerMessage("Incomplete params. params: type:method");
            else if (ScanCommand.PickTarget(The.Player, "invoke", out var pick))
            {
                string typeName = strings[0];
                string methodName = strings[1];
                InvokeWish(typeName, methodName, pick);

            }
        }

        static void InvokeWish(string typeName, string methodName, GameObject pick)
        {
            if (TryReflection(typeName, methodName, pick, out MethodInfo method, out object obj))
                InvokeMethod(method, obj);

        }

        static bool TryReflection(string typeName, string methodName, GameObject pick, out MethodInfo method, out object obj)
        {
            method = null;
            obj = FindObject(pick.PartsList, typeName);
            obj ??= FindObject(pick.Effects, typeName);
            if (obj == null)
            {
                IComponent<GameObject>.AddPlayerMessage($"{pick.DisplayName} ID: {pick.ID} does not have an IPart or Effect named {typeName}");
                return false;
            }
            Type type = obj.GetType();
            if (!TryFindMethod(type, methodName, out method, out string failure))
            {
                failure ??= $"{type.Name} has no method named {methodName}.";
                IComponent<GameObject>.AddPlayerMessage(failure);
                return false;
            }
            return true;
        }

        static void InvokeMethod(MethodInfo method, object obj)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                object[] inputs = new object[parameters.Length];
                for (int i = 0; i < parameters.Length; i++)
                {
                    ParameterInfo param = parameters[i];
                    SimpleToken token = GetSimpleToken(param.ParameterType);
                    string input = GetInput(token, param);
                    ParseInput(token, input, i, inputs, param);
                }
                StringBuilder inputString = new();
                foreach (var input in inputs)
                {
                    inputString.Append($" {input.GetType()} {input}");
                }
                IComponent<GameObject>.AddPlayerMessage($"Invoked {method.Name} with parameter values {inputString}!");
                method.Invoke(obj, inputs);
                return;
            }
            method.Invoke(obj, null);
            IComponent<GameObject>.AddPlayerMessage($"Invoked {method.Name}!");
        }

        static bool TryFindMethod(Type type, string methodName, out MethodInfo method, out string failure)
        {
            method = null;
            failure = null;
            Type limit = GetLimit(type);
            while (type != limit)
            {
                MethodInfo[] methods = type.GetRuntimeMethods().ToArray();
                method = methods.FirstOrDefault(x => x.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
                if (method != null)
                {
                    string name = method.Name;
                    int overloads = methods.Count(x => x.Name == name);
                    if (overloads > 1)
                    {
                        List<MethodInfo> overloadedMethods = GetOverloads(methods, method, overloads);
                        ValidateOverloads(overloadedMethods);
                        if (overloadedMethods.Count == 1)
                            method = overloadedMethods[0];
                        else if (overloadedMethods.Count != 0)
                            method = SortOverloadsForDisplay(overloadedMethods);
                        else
                        {
                            failure = $"Method and all it's overloads have unsupported parameter types.";
                            method = null;
                        }
                    }
                    else
                    {
                        if (!ValidateParameters(method, out string display))
                        {
                            failure = $"Method has unsupported parameter types. {display}";
                            method = null;
                        }
                    }
                    break;
                }
                type = type.BaseType;
            }
            return method != null;
        }

        static List<MethodInfo> GetOverloads(MethodInfo[] methods, MethodInfo method, int overloads)
        {
            List<MethodInfo> overloadedMethods = new(overloads);
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i].Name == method.Name)
                    overloadedMethods.Add(methods[i]);
            }
            return overloadedMethods;
        }
        static void ValidateOverloads(List<MethodInfo> overloadedMethods)
        {
            foreach (var overload in overloadedMethods.ToArray())
            {
                if (!ValidateParameters(overload, out _))
                    overloadedMethods.Remove(overload);

            }
        }
        static MethodInfo SortOverloadsForDisplay(List<MethodInfo> overloadedMethods)
        {
            StringBuilder overloadDisplay = new();
            for (int i = 0; i < overloadedMethods.Count; i++)
            {
                var overload = overloadedMethods[i];
                overloadDisplay.Append($"\n\n {i + 1} {overload.Name}");
                StringBuilder parameterDisplay = ParameterDisplay(overload.GetParameters());

                overloadDisplay.Append($"{parameterDisplay}");
            }
            string display = overloadDisplay.ToString();
            string choice = Popup.AskString($"Multiple overloads detected.\nChoose overload by number: {display}");
            int index = ChoiceLoop(choice, display, overloadedMethods.Count);
            return overloadedMethods[index - 1];
        }

        static bool ValidateParameters(MethodInfo method, out string parameterDisplay)
        {
            ParameterInfo[] parameters = method.GetParameters();
            parameterDisplay = ParameterDisplay(parameters).ToString();
            if (parameters.Length > 0)
            {
                foreach (var param in parameters)
                {
                    SimpleToken token = GetSimpleToken(param.ParameterType);
                    if (token == SimpleToken.Unsupported)
                        return false;
                }
            }
            return true;
        }
        static StringBuilder ParameterDisplay(ParameterInfo[] parameters)
        {
            StringBuilder parameterDisplay = new();
            foreach (var param in parameters)
            {
                parameterDisplay.Append($" {param.ParameterType} {param.Name}");

            }
            return parameterDisplay;
        }


        static void ParseInput(SimpleToken token, string input, int i, object[] inputs, ParameterInfo param)
        {
            bool failedParse = true;
            if (token == SimpleToken.String)
            {
                if (CheckForNull(input, token))
                    inputs[i] = null;
                else
                    inputs[i] = input;
                failedParse = false;
            }
            if (token == SimpleToken.Boolean && !CheckForNull(input, token))
            {
                if (input.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    inputs[i] = false;
                    failedParse = false;
                }
                else if (input.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    inputs[i] = true;
                    failedParse = false;
                }
            }
            if (token == SimpleToken.Int32 && !CheckForNull(input, token))
            {
                if (int.TryParse(input, out int num))
                {
                    inputs[i] = num;
                    failedParse = false;
                }
            }
            if (token == SimpleToken.Int64 && !CheckForNull(input, token))
            {
                if (long.TryParse(input, out long num))
                {
                    inputs[i] = num;
                    failedParse = false;
                }
            }
            if (failedParse)
            {
                Popup.Show($"Could not parse {input} for {param.ParameterType} {param.Name}");
                string newInput = GetInput(token, param);
                ParseInput(token, newInput, i, inputs, param);
            }
        }

        static int ChoiceLoop(string choice, string display, int count)
        {
            if (int.TryParse(choice, out int num))
            {
                if (num > 0 && num <= count)
                    return num;
            }
            choice = Popup.AskString($"Invalid index.\nChoose overload by number: {display}");
            return ChoiceLoop(choice, display, count);
        }


        static string GetInput(SimpleToken token, ParameterInfo param)
        {
            return Popup.AskString($"input {token} value for {param.Name}:");
        }
    }
}