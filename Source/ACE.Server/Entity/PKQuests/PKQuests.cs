using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ACE.Server.Entity.PKQuests
{
    public static class PKQuests
    {
        private static List<PKQuest> _pkQuestList = null;

        public static List<PKQuest> PkQuestList
        {
            get
            {
                if (_pkQuestList == null)
                {
                    _pkQuestList = new List<PKQuest>();

                    //- Participate in 5 arena matches
                    var arena_any_5 = new PKQuest();
                    arena_any_5.QuestCode = "ARENA_ANY_5";
                    arena_any_5.Description = "Participate in 5 Arena matches";
                    arena_any_5.RewardDescription = "20k luminance, 25% XP to next level, 1 Aged Legendary Key, 5 Soul Fragments";
                    arena_any_5.Rewards = new List<string>() { "LUM,20000", "XP%,25", "LEGENDKEY,1", "SOULFRAG,5" };
                    arena_any_5.TaskCount = 5;
                    _pkQuestList.Add(arena_any_5);

                    //- Participate in 15 arena matches
                    var arena_any_15 = new PKQuest();
                    arena_any_15.QuestCode = "ARENA_ANY_15";
                    arena_any_15.Description = "Participate in 15 Arena matches";
                    arena_any_15.RewardDescription = "50k luminance, 50% XP to next level, 3 Aged Legendary Keys, 25 Soul Fragments";
                    arena_any_15.Rewards = new List<string>() { "LUM,50000", "XP%,50", "LEGENDKEY,3", "SOULFRAG,25" };
                    arena_any_15.TaskCount = 15;
                    _pkQuestList.Add(arena_any_15);

                    //- Participate in 30 arena matches
                    var arena_any_30 = new PKQuest();
                    arena_any_30.QuestCode = "ARENA_ANY_30";
                    arena_any_30.Description = "Participate in 30 Arena matches";
                    arena_any_30.RewardDescription = "150k luminance, 100% XP to next level, 3 Aged Legendary Keys, 100 Soul Fragments.";
                    arena_any_30.Rewards = new List<string>() { "LUM,150000", "XP%,100", "LEGENDKEY,3", "SOULFRAG,100" };
                    arena_any_30.TaskCount = 30;
                    _pkQuestList.Add(arena_any_30);

                    //- Participate in 50 arena matches
                    var arena_any_50 = new PKQuest();
                    arena_any_50.QuestCode = "ARENA_ANY_50";
                    arena_any_50.Description = "Participate in 50 Arena matches";
                    arena_any_50.RewardDescription = "200k luminance, 100% XP to next level, 3 Aged Legendary Keys, 100 Soul Fragments.";
                    arena_any_50.Rewards = new List<string>() { "LUM,200000", "XP%,100", "LEGENDKEY,3", "SOULFRAG,100" };
                    arena_any_50.TaskCount = 50;
                    _pkQuestList.Add(arena_any_50);

                    //- Win 10 arena matches
                    var arena_any_win_10 = new PKQuest();
                    arena_any_win_10.QuestCode = "ARENA_ANY_WIN_10";
                    arena_any_win_10.Description = "Win 10 Arena matches";
                    arena_any_win_10.RewardDescription = "30k luminance, 100% XP to next level, 3 Aged Legendary Keys, 40 Soul Fragments";
                    arena_any_win_10.Rewards = new List<string>() { "LUM,30000", "XP%,100", "LEGENDKEY,3", "SOULFRAG,40" };
                    arena_any_win_10.TaskCount = 10;
                    _pkQuestList.Add(arena_any_win_10);

                    //- Win 20 arena matches
                    var arena_any_win_20 = new PKQuest();
                    arena_any_win_20.QuestCode = "ARENA_ANY_WIN_20";
                    arena_any_win_20.Description = "Win 20 Arena matches";
                    arena_any_win_20.RewardDescription = "100k luminance, 100% XP to next level, 3 Aged Legendary Keys, 100 Soul Fragments";
                    arena_any_win_20.Rewards = new List<string>() { "LUM,100000", "XP%,100", "LEGENDKEY,3", "SOULFRAG,100" };
                    arena_any_win_20.TaskCount = 20;
                    _pkQuestList.Add(arena_any_win_20);

                    //- Win 30 arena matches
                    var arena_any_win_30 = new PKQuest();
                    arena_any_win_30.QuestCode = "ARENA_ANY_WIN_30";
                    arena_any_win_30.Description = "Win 30 Arena matches";
                    arena_any_win_30.RewardDescription = "200k luminance, 100% XP to next level, 5 Aged Legendary Keys, 200 Soul Fragments";
                    arena_any_win_30.Rewards = new List<string>() { "LUM,200000", "XP%,100", "LEGENDKEY,5", "SOULFRAG,200" };
                    arena_any_win_30.TaskCount = 30;
                    _pkQuestList.Add(arena_any_win_30);

                    //- Kill 10 players from a whitelisted clan that isn’t your clan(open world or arena)
                    var kill_any_10 = new PKQuest();
                    kill_any_10.QuestCode = "KILL_ANY_10";
                    kill_any_10.Description = "Kill any 10 players from an opposing whitelisted allegiance";
                    kill_any_10.RewardDescription = "80k luminance, 50% XP to next level, 50 Soul Fragments";
                    kill_any_10.Rewards = new List<string>() { "LUM,80000", "XP%,50", "SOULFRAG,50" };
                    kill_any_10.TaskCount = 10;
                    _pkQuestList.Add(kill_any_10);

                    //-Kill 30 players from a whitelisted clan that isn’t your clan(open world or arena)
                    var kill_any_30 = new PKQuest();
                    kill_any_30.QuestCode = "KILL_ANY_30";
                    kill_any_30.Description = "Kill any 30 players from an opposing whitelisted allegiance";
                    kill_any_30.RewardDescription = "200k luminance, 50% XP to next level, 100 Soul Fragments";
                    kill_any_30.Rewards = new List<string>() { "LUM,200000", "XP%,50", "SOULFRAG,100" };
                    kill_any_30.TaskCount = 30;
                    _pkQuestList.Add(kill_any_30);

                    //-Participate in 10 1v1 arena matches
                    var arena_1v1_10 = new PKQuest();
                    arena_1v1_10.QuestCode = "ARENA_1v1_10";
                    arena_1v1_10.Description = "Participate in 10 Arena 1v1 matches";
                    arena_1v1_10.RewardDescription = "20k luminance, 25% XP to next level, 1 Aged Legendary Key, 5 Soul Fragments";
                    arena_1v1_10.Rewards = new List<string>() { "LUM,20000", "XP%,25", "LEGENDKEY,1", "SOULFRAG,5" };
                    arena_1v1_10.TaskCount = 10;
                    _pkQuestList.Add(arena_1v1_10);

                    //- Participate in 20 1v1 arena matches
                    var arena_1v1_20 = new PKQuest();
                    arena_1v1_20.QuestCode = "ARENA_1v1_20";
                    arena_1v1_20.Description = "Participate in 20 Arena 1v1 matches";
                    arena_1v1_20.RewardDescription = "50k luminance, 50% XP to next level, 3 Aged Legendary Keys, 25 Soul Fragments";
                    arena_1v1_20.Rewards = new List<string>() { "LUM,50000", "XP%,50", "LEGENDKEY,3", "SOULFRAG,25" };
                    arena_1v1_20.TaskCount = 20;
                    _pkQuestList.Add(arena_1v1_20);

                    //- Participate in 3 2v2 arena matches
                    var arena_2v2_3 = new PKQuest();
                    arena_2v2_3.QuestCode = "ARENA_2v2_3";
                    arena_2v2_3.Description = "Participate in 3 Arena 2v2 matches";
                    arena_2v2_3.RewardDescription = "20k luminance, 50% XP to next level, 1 Aged Legendary Key, 5 Soul Fragments";
                    arena_2v2_3.Rewards = new List<string>() { "LUM,20000", "XP%,50", "LEGENDKEY,1", "SOULFRAG,5" };
                    arena_2v2_3.TaskCount = 3;
                    _pkQuestList.Add(arena_2v2_3);

                    //- Participate in 10 2v2 arena matches
                    var arena_2v2_10 = new PKQuest();
                    arena_2v2_10.QuestCode = "ARENA_2v2_10";
                    arena_2v2_10.Description = "Participate in 10 Arena 2v2 matches";
                    arena_2v2_10.RewardDescription = "75k luminance, 75% XP to next level, 3 Aged Legendary Keys, 25 Soul Fragments";
                    arena_2v2_10.Rewards = new List<string>() { "LUM,750000", "XP%,75", "LEGENDKEY,3", "SOULFRAG,25" };
                    arena_2v2_10.TaskCount = 10;
                    _pkQuestList.Add(arena_2v2_10);

                    //- Participate in 2 FFA arena match
                    var arena_ffa_2 = new PKQuest();
                    arena_ffa_2.QuestCode = "ARENA_FFA_2";
                    arena_ffa_2.Description = "Participate in 2 Arena FFA matches";
                    arena_ffa_2.RewardDescription = "40k luminance, 25% XP to next level, 1 Aged Legendary Key, 5 Soul Fragments";
                    arena_ffa_2.Rewards = new List<string>() { "LUM,40000", "XP%,25", "LEGENDKEY,1", "SOULFRAG,5" };
                    arena_ffa_2.TaskCount = 2;
                    _pkQuestList.Add(arena_ffa_2);

                    //- Participate in 2 Tugak arena match
                    var arena_tugak_2 = new PKQuest();
                    arena_tugak_2.QuestCode = "ARENA_TUGAK_2";
                    arena_tugak_2.Description = "Participate in 2 Arena Tugak War matches";
                    arena_tugak_2.RewardDescription = "40k luminance, 25% XP to next level, 1 Aged Legendary Key, 5 Soul Fragments";
                    arena_tugak_2.Rewards = new List<string>() { "LUM,40000", "XP%,25", "LEGENDKEY,1", "SOULFRAG,5" };
                    arena_tugak_2.TaskCount = 2;
                    _pkQuestList.Add(arena_tugak_2);

                    //- Participate in 25 Tugak arena match
                    var arena_tugak_25 = new PKQuest();
                    arena_tugak_25.QuestCode = "ARENA_TUGAK_25";
                    arena_tugak_25.Description = "Participate in 25 Arena Tugak War matches";
                    arena_tugak_25.RewardDescription = "120k luminance, 100% XP to next level, 4 Aged Legendary Keys, 250 Soul Fragments";
                    arena_tugak_25.Rewards = new List<string>() { "LUM,120000", "XP%,100", "LEGENDKEY,4", "SOULFRAG,250" };
                    arena_tugak_25.TaskCount = 25;
                    _pkQuestList.Add(arena_tugak_25);

                    //- Participate in 1 group arena match
                    var arena_group_1 = new PKQuest();
                    arena_group_1.QuestCode = "ARENA_GROUP_1";
                    arena_group_1.Description = "Participate in 1 Arena Group match";
                    arena_group_1.RewardDescription = "40k luminance, 50% XP to next level, 2 Aged Legendary Keys, 25 Soul Fragments";
                    arena_group_1.Rewards = new List<string>() { "LUM,40000", "XP%,50", "LEGENDKEY,2", "SOULFRAG,25" };
                    arena_group_1.TaskCount = 1;
                    _pkQuestList.Add(arena_group_1);

                    //- Participate in 3 group arena match
                    var arena_group_3 = new PKQuest();
                    arena_group_3.QuestCode = "ARENA_GROUP_3";
                    arena_group_3.Description = "Participate in 3 Arena Group matches";
                    arena_group_3.RewardDescription = "50k luminance, 50% XP to next level, 4 Aged Legendary Keys, 50 Soul Fragments";
                    arena_group_3.Rewards = new List<string>() { "LUM,50000", "XP%,50", "LEGENDKEY,4", "SOULFRAG,50" };
                    arena_group_3.TaskCount = 3;
                    _pkQuestList.Add(arena_group_3);

                    //- Participate in 10 group arena match
                    var arena_group_10 = new PKQuest();
                    arena_group_10.QuestCode = "ARENA_GROUP_10";
                    arena_group_10.Description = "Participate in 10 Arena Group matches";
                    arena_group_10.RewardDescription = "150k luminance, 100% XP to next level, 5 Aged Legendary Keys, 75 Soul Fragments";
                    arena_group_10.Rewards = new List<string>() { "LUM,150000", "XP%,100", "LEGENDKEY,5", "SOULFRAG,75" };
                    arena_group_10.TaskCount = 10;
                    _pkQuestList.Add(arena_group_10);

                    //- Win 5 1v1 arena matches
                    var arena_1v1_win_5 = new PKQuest();
                    arena_1v1_win_5.QuestCode = "ARENA_1v1_WIN_5";
                    arena_1v1_win_5.Description = "Win 5 Arena 1v1 matches";
                    arena_1v1_win_5.RewardDescription = "20k luminance, % XP to next level, 1 Aged Legendary Key, 5 Soul Fragments";
                    arena_1v1_win_5.Rewards = new List<string>() { "LUM,20000", "XP%,25", "LEGENDKEY,1", "SOULFRAG,5" };
                    arena_1v1_win_5.TaskCount = 5;
                    _pkQuestList.Add(arena_1v1_win_5);

                    //- Win 15 1v1 arena matches
                    var arena_1v1_win_15 = new PKQuest();
                    arena_1v1_win_15.QuestCode = "ARENA_1v1_WIN_15";
                    arena_1v1_win_15.Description = "Win 15 Arena 1v1 matches";
                    arena_1v1_win_15.RewardDescription = "100k luminance, 75% XP to next level, 5 Aged Legendary Key, 50 Soul Fragments";
                    arena_1v1_win_15.Rewards = new List<string>() { "LUM,100000", "XP%,75", "LEGENDKEY,5", "SOULFRAG,50" };
                    arena_1v1_win_15.TaskCount = 15;
                    _pkQuestList.Add(arena_1v1_win_15);

                    //- Win 2 2v2 arena matches
                    var arena_2v2_win_2 = new PKQuest();
                    arena_2v2_win_2.QuestCode = "ARENA_2v2_WIN_2";
                    arena_2v2_win_2.Description = "Win 2 Arena 2v2 matches";
                    arena_2v2_win_2.RewardDescription = "20k luminance, % XP to next level, 1 Aged Legendary Key, 5 Soul Fragments";
                    arena_2v2_win_2.Rewards = new List<string>() { "LUM,20000", "XP%,25", "LEGENDKEY,1", "SOULFRAG,5" };
                    arena_2v2_win_2.TaskCount = 2;
                    _pkQuestList.Add(arena_2v2_win_2);

                    //-Place 1st in an FFA arena match
                    var ARENA_FFA_WIN_1 = new PKQuest();
                    ARENA_FFA_WIN_1.QuestCode = "ARENA_FFA_WIN_1";
                    ARENA_FFA_WIN_1.Description = "Win 1 Arena FFA match";
                    ARENA_FFA_WIN_1.RewardDescription = "20k luminance, % XP to next level, 1 Aged Legendary Key, 5 Soul Fragments";
                    ARENA_FFA_WIN_1.Rewards = new List<string>() { "LUM,20000", "XP%,25", "LEGENDKEY,1", "SOULFRAG,5" };
                    ARENA_FFA_WIN_1.TaskCount = 1;
                    _pkQuestList.Add(ARENA_FFA_WIN_1);

                    //- Place in the top 3 in an FFA arena match
                    var arena_ffa_top3 = new PKQuest();
                    arena_ffa_top3.QuestCode = "ARENA_FFA_TOP3";
                    arena_ffa_top3.Description = "Place in the top 3 in an Arena FFA match";
                    arena_ffa_top3.RewardDescription = "20k luminance, 35% XP to next level, 1 Aged Legendary Key, 5 Soul Fragments";
                    arena_ffa_top3.Rewards = new List<string>() { "LUM,20000", "XP%,35", "LEGENDKEY,1", "SOULFRAG,5" };
                    arena_ffa_top3.TaskCount = 1;
                    _pkQuestList.Add(arena_ffa_top3);

                    //-Place 1st in a Tugak War arena match
                    var ARENA_TUGAK_WIN_1 = new PKQuest();
                    ARENA_TUGAK_WIN_1.QuestCode = "ARENA_TUGAK_WIN_1";
                    ARENA_TUGAK_WIN_1.Description = "Win 1 Arena FFA match";
                    ARENA_TUGAK_WIN_1.RewardDescription = "80k luminance, 75% XP to next level, 2 Aged Legendary Keys, 25 Soul Fragments.";
                    ARENA_TUGAK_WIN_1.Rewards = new List<string>() { "LUM,80000", "XP%,75", "LEGENDKEY,2", "SOULFRAG,25" };
                    ARENA_TUGAK_WIN_1.TaskCount = 1;
                    _pkQuestList.Add(ARENA_TUGAK_WIN_1);

                    //-Place 1st in 20 Tugak War arena matches
                    var ARENA_TUGAK_WIN_20 = new PKQuest();
                    ARENA_TUGAK_WIN_20.QuestCode = "ARENA_TUGAK_WIN_20";
                    ARENA_TUGAK_WIN_20.Description = "Win 1 Arena Tugak War matches";
                    ARENA_TUGAK_WIN_20.RewardDescription = "250k luminance, 200% XP to next level, 6 Aged Legendary Keys, 250 Soul Fragments.";
                    ARENA_TUGAK_WIN_20.Rewards = new List<string>() { "LUM,250000", "XP%,200", "LEGENDKEY,6", "SOULFRAG,250" };
                    ARENA_TUGAK_WIN_20.TaskCount = 20;
                    _pkQuestList.Add(ARENA_TUGAK_WIN_20);

                    //- Place in the top 3 in a Tugak War arena match
                    var arena_tugak_top3 = new PKQuest();
                    arena_tugak_top3.QuestCode = "ARENA_TUGAK_TOP3";
                    arena_tugak_top3.Description = "Place in the top 3 in an Arena Tugak War match";
                    arena_tugak_top3.RewardDescription = "20k luminance, 35% XP to next level, 1 Aged Legendary Key, 5 Soul Fragments";
                    arena_tugak_top3.Rewards = new List<string>() { "LUM,20000", "XP%,35", "LEGENDKEY,1", "SOULFRAG,5" };
                    arena_tugak_top3.TaskCount = 1;
                    _pkQuestList.Add(arena_tugak_top3);

                    //- Win 1 group arena match
                    var arena_group_win_1 = new PKQuest();
                    arena_group_win_1.QuestCode = "ARENA_GROUP_WIN_1";
                    arena_group_win_1.Description = "Win 1 Arena Group match";
                    arena_group_win_1.RewardDescription = "50k luminance, 25% XP to next level, 1 Aged Legendary Key, 50 Soul Fragments";
                    arena_group_win_1.Rewards = new List<string>() { "LUM,50000", "XP%,25", "LEGENDKEY,1", "SOULFRAG,50" };
                    arena_group_win_1.TaskCount = 1;
                    _pkQuestList.Add(arena_group_win_1);

                    //- Win 5 group arena matches
                    var arena_group_win_5 = new PKQuest();
                    arena_group_win_5.QuestCode = "ARENA_GROUP_WIN_5";
                    arena_group_win_5.Description = "Win 5 Arena Group matches";
                    arena_group_win_5.RewardDescription = "100k luminance, 50% XP to next level, 2 Aged Legendary Keys, 75 Soul Fragments";
                    arena_group_win_5.Rewards = new List<string>() { "LUM,100000", "XP%,50", "LEGENDKEY,2", "SOULFRAG,75" };
                    arena_group_win_5.TaskCount = 5;
                    _pkQuestList.Add(arena_group_win_5);

                    //- Win 10 group arena matches
                    var arena_group_win_10 = new PKQuest();
                    arena_group_win_10.QuestCode = "ARENA_GROUP_WIN_10";
                    arena_group_win_10.Description = "Win 10 Arena Group matches";
                    arena_group_win_10.RewardDescription = "150k luminance, 75% XP to next level, 5 Aged Legendary Keys, 100 Soul Fragments";
                    arena_group_win_10.Rewards = new List<string>() { "LUM,150000", "XP%,75", "LEGENDKEY,5", "SOULFRAG,100" };
                    arena_group_win_10.TaskCount = 10;
                    _pkQuestList.Add(arena_group_win_10);

                    //-Do 20k dmg in any type of arena matches
                    var arenaDmg20k = new PKQuest();
                    arenaDmg20k.QuestCode = "ARENA_DMG20K";
                    arenaDmg20k.Description = "Deal 20k PK damage during arena matches";
                    arenaDmg20k.RewardDescription = "Reward = 20k luminance, 25% XP to next level, 1 Aged Legendary Key";
                    arenaDmg20k.TaskCount = 20000;
                    arenaDmg20k.Rewards = new List<string>() { "LUM,20000", "XP%,25", "LEGENDKEY,1" };
                    _pkQuestList.Add(arenaDmg20k);

                    ////- Heal for 5k in any type of arena matches
                    //var arena_heal_5k = new PKQuest();
                    //arena_heal_5k.QuestCode = "ARENA_HEAL5K";
                    //arena_heal_5k.Description = "Heal for 5k health during arena matches";
                    //arena_heal_5k.RewardDescription = "20k luminance, 25% XP to next level, 1 Aged Legendary Key";
                    //arena_heal_5k.TaskCount = 5000;
                    //arena_heal_5k.Rewards = new List<string>() { "LUM,20000", "XP%,25", "LEGENDKEY,1" }
                    //_pkQuestList.Add(arena_heal_5k);

                    //- Receive less than 800 damage as the winner of a single arena match
                    var arena_recdmg_800 = new PKQuest();
                    arena_recdmg_800.QuestCode = "ARENA_RECDMG800";
                    arena_recdmg_800.Description = "Win an arena match while receiving less than 800 damage";
                    arena_recdmg_800.RewardDescription = "20k luminance, 25% XP to next level, 1 Aged Legendary Key";
                    arena_recdmg_800.TaskCount = 1;
                    arena_recdmg_800.Rewards = new List<string>() { "LUM,20000", "XP%,25", "LEGENDKEY,1" };
                    _pkQuestList.Add(arena_recdmg_800);

                    ////- Do 50k dmg against any players from a whitelisted clan that isn’t your clan (open world or arena)
                    //var dmg50k = new PKQuest();
                    //dmg50k.QuestCode = "DMG50K";
                    //dmg50k.Description = "Deal 50k PK damage to members of any whitelisted allegiance that isn't your own";
                    //dmg50k.RewardDescription = "20k luminance, 25% XP to next level, 1 Aged Legendary Key";
                    //dmg50k.TaskCount = 50000;
                    //dmg50k.Rewards = new List<string>() { "LUM,20000", "XP%,25", "LEGENDKEY,1" }
                    //_pkQuestList.Add(dmg50k);
















                    //- Kill 3 players in PK dungeons
                    var pkdungeon_kill_3 = new PKQuest();
                    pkdungeon_kill_3.QuestCode = "PKDUNGEON_KILL_3";
                    pkdungeon_kill_3.Description = "Kill 3 players in PK dungeons";
                    pkdungeon_kill_3.RewardDescription = "25k luminance, 25% XP to next level, 1 Aged Legendary Key, 1 Soul Fragment";
                    pkdungeon_kill_3.TaskCount = 3;
                    pkdungeon_kill_3.Rewards = new List<string>() { "LUM,25000", "XP%,25", "LEGENDKEY,1", "SOULFRAG,1" };
                    _pkQuestList.Add(pkdungeon_kill_3);

                    //- Kill 10 players in PK dungeons
                    var pkdungeon_kill_10 = new PKQuest();
                    pkdungeon_kill_10.QuestCode = "PKDUNGEON_KILL_10";
                    pkdungeon_kill_10.Description = "Kill 10 players in PK dungeons";
                    pkdungeon_kill_10.RewardDescription = "100k luminance, 50% XP to next level, 3 Aged Legendary Keys, 2 Soul Fragments";
                    pkdungeon_kill_10.TaskCount = 10;
                    pkdungeon_kill_10.Rewards = new List<string>() { "LUM,100000", "XP%,50", "LEGENDKEY,3", "SOULFRAG,2" };
                    _pkQuestList.Add(pkdungeon_kill_10);

                    //- Kill 30 players in PK dungeons
                    var pkdungeon_kill_30 = new PKQuest();
                    pkdungeon_kill_30.QuestCode = "PKDUNGEON_KILL_30";
                    pkdungeon_kill_30.Description = "Kill 30 players in PK dungeons";
                    pkdungeon_kill_30.RewardDescription = "250k luminance, 100% XP to next level, 5 Aged Legendary Keys, 5 Soul Fragments";
                    pkdungeon_kill_30.TaskCount = 30;
                    pkdungeon_kill_30.Rewards = new List<string>() { "LUM,250000", "XP%,100", "LEGENDKEY,5", "SOULFRAG,5" };
                    _pkQuestList.Add(pkdungeon_kill_30);

                    //- Deal 50k damage to players in PK dungeons
                    var pkdungeon_dmg_50k = new PKQuest();
                    pkdungeon_dmg_50k.QuestCode = "PKDUNGEON_DMG_50K";
                    pkdungeon_dmg_50k.Description = "Deal 50k damage to players in PK dungeons";
                    pkdungeon_dmg_50k.RewardDescription = "50k luminance, 25% XP to next level, 2 Aged Legendary Keys, 2 Soul Fragments";
                    pkdungeon_dmg_50k.TaskCount = 50000;
                    pkdungeon_dmg_50k.Rewards = new List<string>() { "LUM,50000", "XP%,25", "LEGENDKEY,2", "SOULFRAG,2" };
                    _pkQuestList.Add(pkdungeon_dmg_50k);

                    //- Spend 1 hour in PK dungeons
                    var pkdungeon_time_1h = new PKQuest();
                    pkdungeon_time_1h.QuestCode = "PKDUNGEON_TIME_1H";
                    pkdungeon_time_1h.Description = "Spend 1 hour in PK dungeons";
                    pkdungeon_time_1h.RewardDescription = "30k luminance, 25% XP to next level, 1 Aged Legendary Key, 1 Soul Fragment";
                    pkdungeon_time_1h.TaskCount = 3600; // 1 hour in seconds
                    pkdungeon_time_1h.Rewards = new List<string>() { "LUM,30000", "XP%,25", "LEGENDKEY,1", "SOULFRAG,1" };
                    _pkQuestList.Add(pkdungeon_time_1h);

                    //- Get 10 kills in PK dungeons without dying
                    var pkdungeon_survive = new PKQuest();
                    pkdungeon_survive.QuestCode = "PKDUNGEON_SURVIVE";
                    pkdungeon_survive.Description = "Get 10 kills in PK dungeons without dying";
                    pkdungeon_survive.RewardDescription = "150k luminance, 75% XP to next level, 3 Aged Legendary Keys, 3 Soul Fragments";
                    pkdungeon_survive.TaskCount = 10;
                    pkdungeon_survive.Rewards = new List<string>() { "LUM,150000", "XP%,75", "LEGENDKEY,3", "SOULFRAG,3" };
                    _pkQuestList.Add(pkdungeon_survive);

                    //Kill tasks for new T9 dungeons


                }

                return _pkQuestList;
            }
        }

        public static string[] PKQuests_ParticipateAnyArena = { "ARENA_ANY_5", "ARENA_ANY_15", "ARENA_ANY_30", "ARENA_ANY_50" };

        public static string[] PKQuests_WinAnyArena = { "ARENA_ANY_WIN_10", "ARENA_ANY_WIN_20", "ARENA_ANY_WIN_30" };

        public static string[] PKQuests_Participate1v1Arena = { "ARENA_1v1_10", "ARENA_1v1_20" };

        public static string[] PKQuests_Win1v1Arena = { "ARENA_1v1_WIN_5", "ARENA_1v1_WIN_15" };

        public static string[] PKQuests_Participate2v2Arena = { "ARENA_2v2_3", "ARENA_2v2_10" };

        public static string[] PKQuests_Win2v2Arena = { "ARENA_2v2_WIN_2" };

        public static string[] PKQuests_ParticipateTugakArena = { "ARENA_TUGAK_2", "ARENA_TUGAK_25" };

        public static string[] PKQuests_WinTugakArena = { "ARENA_TUGAK_WIN_1", "ARENA_TUGAK_WIN_20" };

        public static string[] PKQuests_ParticipateGroupArena = { "ARENA_GROUP_1", "ARENA_GROUP_3", "ARENA_GROUP_10" };

        public static string[] PKQuests_WinGroupArena = { "ARENA_GROUP_WIN_1", "ARENA_GROUP_WIN_5", "ARENA_GROUP_WIN_10" };

        public static string[] PKQuests_KillAnywhere = { "KILL_ANY_10", "KILL_ANY_30" };

        public static string[] PKQuests_PKDungeonKills = { "PKDUNGEON_KILL_3", "PKDUNGEON_KILL_10", "PKDUNGEON_KILL_30" };

        public static string[] PKQuests_PKDungeonMisc = { "PKDUNGEON_DMG_50K", "PKDUNGEON_TIME_1H", "PKDUNGEON_SURVIVE" };



        public static PKQuest GetPkQuestByCode(string questCode)
        {
            return PKQuests.PkQuestList.FirstOrDefault(x => x.QuestCode.Equals(questCode));
        }
    }

    public class PKQuest
    {
        public string QuestCode { get; set; }

        public string Description { get; set; }

        public string RewardDescription { get; set; }

        public int TaskCount { get; set; }

        public List<string> Rewards { get; set; }
    }

    public class PlayerPKQuest
    {
        public string QuestCode { get; set; }

        public int TaskDoneCount { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime? CompletedTime { get; set; }

        public bool IsCompleted { get; set; }

        public bool RewardDelivered { get; set; }
    }
}


