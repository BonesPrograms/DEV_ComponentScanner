using System;
using XRL;
using XRL.World;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using XRL.World.Parts;
using ObjectInformation;

namespace BeastScanner
{


    //This does not read through the fields of GameObject types because fields like Zone and Cell will DESTROY the console
    //Instead GameObject Scanner is used when a GameObject is detected as a field in a component
    //GameObject elements in Collections are read "shallow" to avoid clogging console

    //If you want to read a part from a GameObject that only exists as a field in your component, you will need to instance this class in code
    //and pass the desired part from your GameObject field
    //such as mutation equipment

    //In the future I may add a way for you to name the field and one of its parts so that it can be read, but for now this is what you get

    //You can use "ReadClass" arbitrarily, you can send any type through the scanner, even if it is not an IComponent<GameObject>
    //but you will have to do that in code

    //Parts like Body, Brain and Physics cannot be completely read, because it would become very unreadable
    //Declared fields will be read, but as member access chains increase, object information will be skipped
    //If you want to read specific details like that, you need to instance this type in code

    //LoopLimit can be null - will stop reading at System.Object
    //if reading a component in code, should mark this as typeof(IPart) or typeof(Effect) otherwise you will read the parentobject and it will clog console

    public class ComponentScanner
    {

        public Type LoopLimit;

       public const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly | BindingFlags.Static;
        //loops through all base types so its declared only, but "source type" is tracked (the inheritor at the very end) if its a Field

        public ComponentScanner(Type loopLimit)
        {
            LoopLimit = loopLimit;
        }
        public void Scan<T>(T component, GameObject basis = null, int skip = 25) where T : IComponent<GameObject>
        {
            basis ??= component.GetBasisGameObject();
            Skip(skip);
            string msg = $"Beginning read of fields in {component.GetType()} on {basis?.DisplayName} ID {basis?.ID}";
            MetricsManager.LogInfo(msg);
            Skip(2);
            ReadClass(component);
        }
        public void ReadClass(object classObj, bool cameFromCollection = false, bool cameFromReferenceType = false)
        {
            Type objectType = classObj.GetType();
            MetricsManager.LogInfo($"Beginning read for fields of reference type {objectType}.");
            Skip(1);
            LoopInheritance(classObj, objectType, cameFromCollection, cameFromReferenceType);
        }

        void LoopInheritance(object classObj, Type sourceType, bool cameFromCollection, bool cameFromReferenceType)
        {
            Type varyingType = sourceType;
            while (varyingType != LoopLimit && varyingType != null)
            {
                FieldInfo[] fields = varyingType.GetFields(Flags);
                List<Field> sortedFields = SortFields(classObj, fields, sourceType);
                foreach (Field field in sortedFields)
                    ReadObject(field, field.FieldInfo, field.SourceType, cameFromCollection, cameFromReferenceType);
                varyingType = varyingType.BaseType;
            }
        }


        //cameFromCollection is a little ambiguous here : it does not mean this object is an element in a collection
        //that is what isInCollection means (because we are receiving an <Element> object rather than a <Field> object)
        //cameFromCollection means we are reading a field in a reference type that is an element in a collection
        void ReadObject(ObjectInfo info, FieldInfo field, Type sourceType, bool cameFromCollection, bool cameFromReferenceType)
        {
            Skip(1);
            object obj = info.Object;
            Token token = info.Token;
            bool isInCollection = info is Element; //field and sourcetype will be null
            ReadObjectBasic(info, field, sourceType, isInCollection);
            switch (token)
            {
                case Token.IPart or Token.Effect: //i use a lot of iparts as fields and i do not want to read it in annoying member access chains when i could just query it directly
                    {
                        if (cameFromReferenceType || isInCollection || cameFromCollection)
                            goto case Token.ReferenceType;
                        else
                            MetricsManager.LogInfo($"\"{field.Name}\" is an {token}, use {token} scanner directly on target GameObject to read {token} data or instance ComponentScanner in code to read it.");
                    }
                    break;
                case Token.IDictionary:
                    {
                        if (cameFromReferenceType)
                            goto case Token.ReferenceType;
                        if (!isInCollection && !cameFromCollection)
                            ReadIDictionary(obj as IDictionary);
                        else
                            MetricsManager.LogInfo("Detected a collection as an element in a collection or a field for a reference type in a collection, skipping for readability.");
                    }
                    break;
                case Token.IList:
                    {
                        if (cameFromReferenceType)
                            goto case Token.ReferenceType;
                        if (!isInCollection && !cameFromCollection)     //reading collections inside of collections is not easy to understand so it is not allowed
                            ReadIList(obj as IList);
                        else
                            MetricsManager.LogInfo("Detected a collection as an element in a collection or a field for a reference type in a collection, skipping for readability.");
                    }
                    break;
                case Token.GameObject:
                    {
                        if (cameFromReferenceType)
                            goto case Token.ReferenceType;
                        if (!isInCollection && !cameFromCollection)
                        {
                            MetricsManager.LogInfo($"\"{field.Name}\" is a GameObject, using GameObject Scanner.");
                            Skip(3);
                            new GameObjectDataRecord(obj as GameObject).ReadData(0);
                            Skip(3);
                        }
                        else
                        {
                            if (cameFromCollection)
                                MetricsManager.LogInfo($"{(isInCollection ? "Element" : $"\"{field.Name}\"")} is a GameObject in a multiple-collection access chain, skipping for sanity.");
                            else if (isInCollection)
                            {
                                MetricsManager.LogInfo("Element is a GameObject, performing shallow read."); //reading gameobjects in collections with the scanner would clog log
                                ReadGameObjectShallow(obj as GameObject);
                            }
                        }
                    }
                    break;
                case Token.ReferenceType:
                    {
                        if (cameFromReferenceType) //member access chains that are larger than one are really hard to keep track of in your brain when reading the log, so they are skipped
                            MetricsManager.LogInfo("Detected reference type as a field in a reference type. Skipping for readability."); //they also clog the console
                        else if (!cameFromCollection)
                        {
                            MetricsManager.LogInfo($"{(isInCollection ? "element" : $"\"{field.Name}\"")} is a reference type, reading fields.");
                            ReadClass(obj, isInCollection, true);
                        }
                        else if (!isInCollection)
                            MetricsManager.LogInfo($"{$"\"{field.Name}\""} is a reference type, but is a field for a reference type that is an element in a collection. Skipping for readability.");
                        else
                            MetricsManager.LogInfo("Detected reference type element in a multi-collection chain. Skipping for readability.");
                    }       //parts like Brain will make the console EXPLODE if we read these, some of their collections have deep member access chains, such as PartyMembers
                    break;  //you cant really read those collections properly with this Type at all
            }

        }

        static void ReadObjectBasic(ObjectInfo info, FieldInfo field, Type sourceType, bool isInCollection)
        {
            StringBuilder text = new(); //sourceType and field will be null if in collection
            text.Append($"Reading {(isInCollection ? "ELEMENT" : $"FIELD \"{field.Name}\"")} from {(isInCollection ? "collection" : $"type {sourceType}")}:\n");
            text.Append($"Type: {(isInCollection ? info.IsNull ? "null value in keyValuePair, cannot get type info" : info.Object.GetType() : field.FieldType)}\n");
            text.Append(DisplayValue(info)); //I have prevented lists from sending null elements, and IDictionary from sending null keys, but keys with null values are permitted
            if (!isInCollection)                //so in those cases it is possible for us to be unable to retrieve the value's type
            {                                   //though you can just see the dictionary's generic arguments to get an idea of what type it would've been
                text.Append($"Declared in: {field.DeclaringType}\n");
                text.Append($"Attributes {field.Attributes}\n");
            }
            MetricsManager.LogInfo(text.ToString());

        }

        static void ReadGameObjectShallow(GameObject obj)
        {
            StringBuilder text = new();
            text.Append($"DisplayName: {obj.DisplayName}\n");
            if (int.TryParse(obj.ID, out int id))
                text.Append($"ID: {id}\n");
            text.Append($"Blueprint: {obj.Blueprint}\n");
            text.Append($"Level: {obj.Level}\n");
            text.Append($"HP: {obj.baseHitpoints}\n");
            if (obj.CurrentCell != null)
            {
                text.Append($"Cell Coordinates: X: {obj.CurrentCell.X} Y: {obj.CurrentCell.Y}\n");
                text.Append($"Direction from your cell: {The.Player.GetDirectionToward(obj)}\n");
                text.Append($"Distance from your cell: {The.Player.DistanceTo(obj)}\n"); //incase you want to walk up to them for a deeper analysis
            }
            MetricsManager.LogInfo(text.ToString());
        }

        void ReadIList(IList list)
        {
            Skip(1);
            MetricsManager.LogInfo("READING ILIST OF COUNT " + list.Count.ToString());
            List<Element> sortedElements = SortElements(list);
            foreach (var element in sortedElements)
            {
                ReadObject(element, null, null, false, false);
            }
            Skip(1);
            MetricsManager.LogInfo("ILIST FINISHED");
            Skip(1);
        }

        void ReadIDictionary(IDictionary dic)
        {
            Skip(1);
            MetricsManager.LogInfo("READING IDICTIONARY OF COUNT " + dic.Count.ToString());
            IDictionaryEnumerator enumerator = dic.GetEnumerator();
            Dictionary<Element, Element> elementDictionary = new();
            while (enumerator.MoveNext())
            {
                if (enumerator.Key != null)
                {
                    Token keytoken = ObjectInfo.GetToken(enumerator.Key);
                    Token valuetoken = ObjectInfo.GetToken(enumerator.Value);
                    elementDictionary[new Element(keytoken, enumerator.Key)] = new Element(valuetoken, enumerator.Value);
                }
            }
            int count = 1;
            foreach (var pair in elementDictionary)
            {
                Skip(1);
                MetricsManager.LogInfo($"KEY: {count}");
                ReadObject(pair.Key, null, null, false, false);
                Skip(1);
                MetricsManager.LogInfo($"VALUE: {count}");
                ReadObject(pair.Value, null, null, false, false);
                count++;
            }
            Skip(1);
            MetricsManager.LogInfo("IDICTIONARY FINISHED");
            Skip(1);

        }


        static List<Field> SortFields(object classObj, FieldInfo[] fields, Type sourceType)
        {
            List<Field> fieldDetails = new(fields.Length);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo field = fields[i];
                object fieldObj = field.GetValue(classObj);
                Token token = ObjectInfo.GetToken(fieldObj);
                fieldDetails.Add(new Field(token, field, fieldObj, sourceType));
            }
            return SortByToken(fieldDetails);
        }

        static List<Element> SortElements(IList list) //on the off chance you have a List<object>
        {                                       //you cant control dictionary order and ObjectInfo doesn't support making a "KeyValue" class for a theoretical List<KeyValue> (i tried it, its a mess)
            List<Element> elements = new();     //so we dont sort dictionaries by order
            for (int i = 0; i < list.Count; i++)
            {
                object element = list[i];
                if (element != null)
                {
                    Token token = ObjectInfo.GetToken(element);
                    elements.Add(new Element(token, element));
                }
            }
            return SortByToken(elements);
        }

        static List<O> SortByToken<O>(List<O> infoObjects) where O : ObjectInfo
        {
            List<O> referenceTypes = new();
            List<O> simpleTypes = new();
            List<O> collections = new();
            List<O> gameObjects = new();
            foreach (var info in infoObjects)
            {
                if (info.IsGameObject)
                    gameObjects.Add(info);
                else if (info.IsCollection)
                    collections.Add(info);
                else if (info.IsSimple || info.IsNull)
                    simpleTypes.Add(info);
                else if (info.IsReferenceType)
                    referenceTypes.Add(info);
            }
            List<O> sortedInfo = new(infoObjects.Count);
            sortedInfo.AddRange(simpleTypes);
            sortedInfo.AddRange(collections);
            sortedInfo.AddRange(gameObjects);
            sortedInfo.AddRange(referenceTypes);
            return sortedInfo; //rearranges order
        }


        static string DisplayValue(ObjectInfo info)
        {
            string msg = $"Value: {info.Object ?? "null"}\n";
            if (info.IsSimple && !info.IsNull)
            {

                string valueDisplay = SimpleValueDisplay(info.Object, info.Token);
                msg = $"Value: {valueDisplay}\n";
            }
            return msg;

        }


        static string SimpleValueDisplay(object obj, Token token) =>
        token switch
        {
            Token.Boolean => ShowBoolValue((bool)obj),
            Token.String or Token.Enum => $"\"{obj}\"", //gives strings an enums an orange stringy color
            _ => $"{obj}", //integers always look blue and custom structs have the option of string overloads
        };

        static string ShowBoolValue(bool boolean)
        {
            return boolean ? "true" : "false"; //because the string literal values of booleans are uncolored in the log
        }                                       //its kind of funny you have to do this manually, le trolled

        static void Skip(int value)
        {
            for (int i = 0; i < value; i++)
                MetricsManager.LogInfo("\n");
        }
    }

    
}