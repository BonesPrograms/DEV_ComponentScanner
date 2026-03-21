using System;
using XRL.World;
using System.Collections;
using System.Reflection;

namespace ObjectInformation
{
    public class ObjectInfo
    {
        public readonly Token Token;

        public readonly object Object;

        public ObjectInfo(Token token, object Object)
        {
            this.Token = token;
            this.Object = Object;
        }

        public bool IsNull => Token == Token.Null;

        public bool IsGameObject => Token == Token.GameObject;

        public bool IsReferenceType => Token == Token.ReferenceType || Token == Token.IPart || Token == Token.Effect;

        public bool IsCollection => Token == Token.IList || Token == Token.IDictionary;

        public bool IsSimple
        => Token switch
        {
            Token.Struct or Token.String or Token.Enum or Token.Delegate or Token.Boolean => true,
            _ => false
        };

        public static Token GetToken(object obj)
         =>
            obj switch
            {
                null => Token.Null,
                string => Token.String,
                bool => Token.Boolean,
                Enum => Token.Enum,
                ValueType => Token.Struct,
                IDictionary => Token.IDictionary,
                IList => Token.IList,
                GameObject => Token.GameObject,
                Effect => Token.Effect,
                IPart => Token.IPart,
                Delegate => Token.Delegate, //this may require extra work, dont know how to read the delegate value yet
                _ => Token.ReferenceType
            };


    }

    public class Element : ObjectInfo
    {
        public Element(Token token, object Object) : base(token, Object)
        {
        }
    }

    public class Field : ObjectInfo
    {
        public readonly FieldInfo FieldInfo;

        public readonly Type SourceType; //the type of the instance that the field's value "exists" in (debatable term if the field is private and was declared in a base class)
                                         //that being said SourceType obviously may be different from the field's actual declaring type 
        public Field(Token token, FieldInfo field, object Object, Type sourceType) : base(token, Object)
        {
            FieldInfo = field;
            SourceType = sourceType;
        }


    }

    public enum Token  //this is a playtime kiddy sandbox lollipop version of the serializationType enum lol
    {
        Uninitialized, //unused default value
        Null,
        IList,
        IDictionary,
        Struct,
        String,
        ReferenceType,
        Enum,
        Delegate,
        GameObject,
        Boolean,
        IPart,
        Effect
    }

    public enum SimpleToken
    {
        Unsupported,
        String,
        Boolean,
        Int32,
        Int64

    }
}