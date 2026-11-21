using System;
using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Localization;
using TaleWorlds.SaveSystem;

namespace CasusBelli.Managers.Titles
{
    // Ação de concessão de terras (estate)
    public class EstateAction
    {
        public EstateAction(FeudalTitle title, Hero actionTaker, Hero actionTarget)
        {
            Title = title;
            ActionTaker = actionTaker;
            ActionTarget = actionTarget;
            Influence = 20f;
        }

        public FeudalTitle Title { get; private set; }
        public Hero ActionTaker { get; private set; }
        public Hero ActionTarget { get; private set; }
        public float Influence { get; set; }
    }

    // Estate (terra/propriedade)
    public class Estate
    {
        [SaveableProperty(1)] public string StringId { get; private set; }
        [SaveableProperty(2)] public Settlement Settlement { get; private set; }
        [SaveableProperty(3)] public Hero Owner { get; private set; }
        [SaveableProperty(4)] public float Value { get; private set; }

        public Estate(string stringId, Settlement settlement, Hero owner, float value)
        {
            StringId = stringId;
            Settlement = settlement;
            Owner = owner;
            Value = value;
        }

        public void SetOwner(Hero newOwner)
        {
            Owner = newOwner;
        }
    }
}