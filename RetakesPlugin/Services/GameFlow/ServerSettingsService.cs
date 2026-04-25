using CounterStrikeSharp.API;
using RetakesPlugin.Core;

namespace RetakesPlugin.Services.GameFlow
{
    public class ServerSettingsService
    {
        public void EnsureApplied()
        {
            Apply();
        }

        private static void Apply()
        {
            Server.ExecuteCommand("mp_freezetime 3");
            Server.ExecuteCommand("mp_c4timer 40");
            Server.ExecuteCommand("mp_roundtime_defuse 2");
            Server.ExecuteCommand("mp_warmuptime 5");

            Server.ExecuteCommand("mp_teammates_are_enemies 0");
            Server.ExecuteCommand("mp_autoteambalance 0");
            Server.ExecuteCommand("mp_limitteams 0");
            Server.ExecuteCommand("mp_ignore_round_win_conditions 0");
            Server.ExecuteCommand("mp_give_player_c4 0");
            Server.ExecuteCommand("mp_halftime 0");
            Server.ExecuteCommand("mp_halftime_duration 0");
            Server.ExecuteCommand("mp_match_can_clinch 0");
            Server.ExecuteCommand("mp_maxrounds 30");
            Server.ExecuteCommand("bot_quota 1");
            Server.ExecuteCommand("bot_difficulty 3");
            Server.ExecuteCommand("bot_defer_to_human_goals 0");
            Server.ExecuteCommand("bot_defer_to_human_items 0");
            Server.ExecuteCommand("bot_chatter off");
            Server.ExecuteCommand("bot_allow_grenades 1");
            Server.ExecuteCommand("bot_join_after_player 0");
            Server.ExecuteCommand("sv_autobunnyhopping 1");
            Server.ExecuteCommand("sv_enablebunnyhopping 1");
            Server.ExecuteCommand("mp_buytime 0");
            Server.ExecuteCommand("mp_buy_anywhere 0");
            Server.ExecuteCommand("mp_buy_during_immunity 0");
            Server.ExecuteCommand("mp_startmoney 0");
            Server.ExecuteCommand("mp_maxmoney 0");
        }
    }
}
