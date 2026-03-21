using XRL.Wish;
using XRL.World;
using System;
using System.Collections.Generic;
using XRL;
using System.Linq;

namespace BeastScanner
{


    [HasWishCommand]
    internal class ScanCommand : BaseReflective
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

        // static bool CheckList<T>(IList<T> list, string name, out T component) where T : IComponent<GameObject>
        // {
        //     component = list.FirstOrDefault(x => x.GetType().Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        //     return component != null;
        // }

        static void ScanWish<T>(string name, string action, Func<GameObject, IList<T>> expr) where T : IComponent<GameObject>
        {
            if (PickTarget(The.Player, action, out GameObject pick))
            {
                string baseClass = typeof(T).Name;
                IList<T> list = expr(pick);
                T component = FindObject(list, name);
                if (component != null)
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