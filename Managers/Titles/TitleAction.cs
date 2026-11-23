using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace CasusBelli.Managers.Titles
{
    // Ação que pode ser tomada sobre um título
    public class TitleAction
    {
        public TitleAction(ActionType type, FeudalTitle title, Hero actionTaker)
        {
            Type = type;
            Title = title;
            ActionTaker = actionTaker;
            Vassals = new List<FeudalTitle>();
        }

        [SaveableProperty(1)] public float Gold { get; set; }
        [SaveableProperty(2)] public float Renown { get; set; }
        [SaveableProperty(3)] public float Influence { get; set; }
        [SaveableProperty(4)] public ActionType Type { get; private set; }
        [SaveableProperty(5)] public FeudalTitle Title { get; private set; }
        [SaveableProperty(6)] public List<FeudalTitle> Vassals { get; private set; }
        [SaveableProperty(7)] public Hero ActionTaker { get; private set; }
        [SaveableProperty(8)] public bool Possible { get; set; }
        [SaveableProperty(9)] public TextObject Reason { get; set; }

        public bool IsHostile()
        {
            return Type is ActionType.Usurp or ActionType.Claim or ActionType.Revoke;
        }

        public bool IsWilling
        {
            get
            {
                bool result = true;
                if (IsHostile())
                {
                    Hero target = Title.deJure;
                    if (target == null) return true;

                    int ambitious = ActionTaker.GetTraitLevel(DefaultTraits.Calculating);
                    if (ambitious >= 1)
                    {
                        if (ambitious == 2)
                        {
                            result = true;
                        }
                        else if (ActionTaker.GetRelation(target) < 50)
                        {
                            result = true;
                        }
                    }

                    if (ActionTaker.MapFaction != target.MapFaction)
                    {
                        result = true;
                    }

                    if (ActionTaker.IsFriend(target))
                    {
                        result = false;
                    }
                    else if (ActionTaker.IsEnemy(target))
                    {
                        result = true;
                    }

                    if (result && target.MapFaction == ActionTaker.MapFaction)
                    {
                        result = false;
                        if (MBRandom.RandomFloat < 0.01f) result = true;
                    }
                }

                if (Gold >= ActionTaker.Gold * 0.5f)
                {
                    result = false;
                }

                return result;
            }
        }

        public void SetTitle(FeudalTitle title)
        {
            Title = title;
        }

        public void SetVassals(List<FeudalTitle> vassals)
        {
            Vassals = vassals;
        }

        public void TakeAction(Hero receiver)
        {
            if (!Possible)
            {
                return;
            }

            var manager = TitleManager.Instance;
            if (manager == null) return;

            switch (Type)
            {
                case ActionType.Usurp:
                    // Revalidate even if this action was created before ownership or resources changed.
                    var usurp = manager.GetModel().GetUsurpAction(Title, ActionTaker);
                    if (usurp.Possible) manager.UsurpTitle(Title.deJure, usurp);
                    break;
                case ActionType.Claim:
                    manager.AddOngoingClaim(this);
                    break;
                case ActionType.Revoke:
                    manager.RevokeTitle(this);
                    break;
                case ActionType.Create:
                    manager.CreateTitle(this);
                    break;
                case ActionType.Found:
                    TitleGenerator.FoundKingdom(this);
                    break;
                case ActionType.Grant:
                    if (manager.GetModel().GetGrantAction(Title,ActionTaker,receiver).Possible) manager.GrantTitle(this, receiver);
                    break;
                case ActionType.Destroy:
                    var check = manager.GetModel().GetDestroyAction(Title,ActionTaker);
                    if (!check.Possible) return;
                    ActionTaker.Clan.Influence -= check.Influence;
                    ActionTaker.Clan.Renown -= check.Renown;
                    manager.DeactivateTitle(Title);
                    break;
            }
        }
    }
}