using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using System.Linq;

namespace RetakesPlugin.Services.GameFlow
{
    public class LoadoutService
    {
        public void RemovePlayerWeapons(CCSPlayerPawn pawn)
        {
            var weaponServices = pawn.WeaponServices;
            if (weaponServices?.MyWeapons == null) return;

            var weapons = weaponServices.MyWeapons.ToList();
            foreach (var w in weapons)
            {
                if (w?.Value != null && w.Value.IsValid) w.Value.Remove();
            }
        }

        public void GiveLoadout(CCSPlayerController player)
        {
            player.GiveNamedItem("weapon_knife");
            player.GiveNamedItem("item_assaultsuit");

            if (player.TeamNum == 2)
            {
                player.GiveNamedItem("weapon_ak47");
                player.GiveNamedItem("weapon_glock");
            }
            else if (player.TeamNum == 3)
            {
                player.GiveNamedItem("weapon_m4a1_silencer");
                player.GiveNamedItem("weapon_usp_silencer");
                player.GiveNamedItem("item_defuser");
            }

            Server.NextFrame(() =>
            {
                player.ExecuteClientCommand("slot1");
            });
        }
    }
}
