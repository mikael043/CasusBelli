using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli
{
    public partial class CasusBelliKingBehavior
    {
        public List<NegotiationOffer> GetOffers(KingCaptureRecord record, Settlement requested = null)
        {
            List<NegotiationOffer> offers = new List<NegotiationOffer>();

            CasusBelliCampaignBehavior main = GetMain();
            Hero king = FindHero(record.KingHeroId);
            Kingdom captive = GetKingdom(record.KingKingdomId);
            Kingdom captor = GetKingdom(record.CaptorKingdomId);

            if (main == null || king == null || captive == null || captor == null || record.IsResolved ||
                captive.Leader != king || king.PartyBelongedToAsPrisoner?.MapFaction != captor ||
                !captor.IsAtWarWith(captive) ||
                (captive.RulingClan.Influence > 0 && (CampaignTime.Now.ToDays < record.NextAttempt ||
                 CampaignTime.Now.ToDays - record.CapturedOnDay >= ProtectionDays)))
            {
                return offers;
            }

            KingdomProfile captorProfile = KingdomProfiles.Get(captor.StringId);
            KingdomProfile captiveProfile = KingdomProfiles.Get(captive.StringId);

            // 1) Cumprir o objetivo de guerra
            NegotiationOffer objective = new NegotiationOffer();
            objective.Type = NegotiationOfferType.Objective;
            objective.Title = "Exigir o objetivo de guerra";
            objective.WillAccept = true;

            WarRecord war;
            Settlement target = null;

            if (main.TryGetActiveWarBetween(
                    captor.StringId,
                    captive.StringId,
                    out war) &&
                war.IsTerritorial &&
                war.AttackerKingdomId == captor.StringId)
            {
                target = Settlement.Find(war.TargetSettlementId);
            }

            if (war == null || war.CasusBelliType == "UNASSIGNED" || string.IsNullOrEmpty(war.CasusBelliType))
            {
                var eligible = RoyalSettlement.EligibleFiefs(captor, captive, war);
                target = requested == null ? eligible.FirstOrDefault() : eligible.Contains(requested) ? requested : null;
                objective.Title = "Exigir feudo de fronteira";
                objective.WillAccept = target?.OwnerClan?.Kingdom == captor || target != null && captive.Fiefs.Count > 1 &&
                    (record.Refusals > 0 || !target.IsTown) && captive.Fiefs.Count >= captor.Fiefs.Count / 2;
            }
            objective.Target = target;
            if (target != null && (target.OwnerClan?.Kingdom == captive || target.OwnerClan?.Kingdom == captor))
            {
                objective.IsEnabled = true;
                objective.Detail = target.OwnerClan?.Kingdom == captor ? $"Confirmar a posse de {target.Name} e encerrar a guerra." : target.Name + " passa ao soberano, o rei " +
                    "e libertado e a guerra termina em paz.";
            }
            else
            {
                objective.IsEnabled = false;
                objective.DisabledReason =
                    "Nao ha objetivo de guerra pendente contra este reino.";
            }

            offers.Add(objective);

            // 2 e 3) Resgates
            int payable = GetPayableGold(king);
            int baseRansom = GetBaseRansom(king);
            float acceptableMultiple =
                AcceptableMultiplePerPayFactor * captiveProfile.RansomPayFactor;

            offers.Add(BuildRansomOffer(
                NegotiationOfferType.RansomModerate,
                "Resgatar o soberano e continuar a guerra",
                ModerateRansomMultiple,
                baseRansom,
                payable,
                captorProfile,
                acceptableMultiple));

            offers.Add(BuildRansomOffer(
                NegotiationOfferType.RansomHeavy,
                "Exigir indenização pela paz",
                HeavyRansomMultiple + Math.Min(4, record.Refusals) * 0.5f,
                baseRansom,
                payable,
                captorProfile,
                acceptableMultiple));

            if (record.Refusals > 0)
            {
                var counter = offers[offers.Count - 1];
                counter.Title = "Aceitar contraproposta de resgate";
                counter.IsEnabled = counter.Amount > offers[offers.Count - 2].Amount;
                counter.WillAccept = counter.IsEnabled;
                counter.DisabledReason = "O reino não consegue reunir uma contraproposta superior ao resgate moderado.";
                counter.Detail = $"O reino oferece {counter.Amount} denários para conservar o feudo.";
            }
            // 4) Clemencia
            // [NOVA OFERTA 2/5] Cada oferta nova e montada aqui, na ordem
            // em que deve aparecer no texto da negociacao.
            NegotiationOffer mercy = new NegotiationOffer();
            mercy.Type = NegotiationOfferType.Mercy;
            mercy.Title = "Libertar por clemencia";
            mercy.IsEnabled = true;
            mercy.WillAccept = true;
            mercy.Detail = "O rei e libertado sem pagar nada e a guerra " +
                "termina em paz. O reino dele nao guarda rancor.";
            offers.Add(mercy);

            if (captive.RulingClan.Influence <= 0)
                foreach (var offer in offers) offer.WillAccept = true;
            return offers;
        }

        private NegotiationOffer BuildRansomOffer(
            NegotiationOfferType type,
            string title,
            float multiple,
            int baseRansom,
            int payable,
            KingdomProfile captorProfile,
            float acceptableMultiple)
        {
            int demanded = (int)(baseRansom * multiple *
                captorProfile.RansomDemandFactor);

            NegotiationOffer offer = new NegotiationOffer();
            offer.Type = type;
            offer.Title = title;
            offer.Amount = Math.Min(demanded, payable);
            offer.WillAccept = multiple <= acceptableMultiple;

            if (payable <= 0)
            {
                offer.IsEnabled = false;
                offer.DisabledReason = "Os lordes do reino não têm ouro para pagar.";
                return offer;
            }

            offer.IsEnabled = true;
            offer.Detail = "Exige " + demanded + " denarios" +
                (demanded > payable
                    ? " (o reino só consegue reunir " + payable + ")"
                    : "") +
                ". Reacao provavel: " +
                (offer.WillAccept ? "aceita" : "recusa") + ".";

            return offer;
        }

        internal void PlayerOffer(KingCaptureRecord record, NegotiationOfferType type, Action<string> done)
        {
            var captive = GetKingdom(record.KingKingdomId);
            var captor = GetKingdom(record.CaptorKingdomId);
            if (!IsPlayerSovereignOf(captor) || captive == null) return;
            var main = GetMain();
            if (type == NegotiationOfferType.Objective && main != null &&
                (!main.TryGetActiveWarBetween(captor.StringId, captive.StringId, out var war) ||
                 string.IsNullOrEmpty(war.CasusBelliType) || war.CasusBelliType == "UNASSIGNED"))
            {
                var choices = RoyalSettlement.EligibleFiefs(captor, captive, war)
                    .OrderBy(s => s.Name.ToString()).Select(s => new InquiryElement(s, s.Name.ToString(), null)).ToList();
                if (choices.Count == 0) return;
                MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData("Exigir um feudo",
                    "Escolha o feudo que o reino cativo deverá ceder pela paz.", choices, true, 1, 1,
                    "Exigir", "Cancelar", selected =>
                    {
                        if (selected.Count != 1 || !IsPlayerSovereignOf(captor)) return;
                        ExecuteOffer(record, type, out var result, (Settlement)selected[0].Identifier);
                        done(result);
                    }, selected => { }));
                return;
            }
            ExecuteOffer(record, type, out var message);
            done(message);
        }

        public bool ExecuteOffer(
            KingCaptureRecord record,
            NegotiationOfferType type,
            out string message, Settlement requested = null)
        {
            message = "";

            CasusBelliCampaignBehavior main = GetMain();
            Hero king = FindHero(record.KingHeroId);
            Kingdom captive = GetKingdom(record.KingKingdomId);
            Kingdom captor = GetKingdom(record.CaptorKingdomId);

            if (record.IsResolved ||
                main == null ||
                king == null ||
                captive == null ||
                captor == null ||
                !king.IsPrisoner)
            {
                message = "negociacao indisponivel";
                return false;
            }

            // O soberano do reino captor representa os lordes na negociação.
            Hero decider = captor.Leader;

            if (decider == null)
            {
                message = "o reino captor nao tem soberano";
                return false;
            }


            NegotiationOffer offer = GetOffers(record, requested).Find(o => o.Type == type);

            if (offer == null || !offer.IsEnabled)
            {
                message = offer != null ? offer.DisabledReason : "oferta invalida";
                return false;
            }

            if (type == NegotiationOfferType.Objective)
            {
                if (!offer.WillAccept && !IsPlayerSovereignOf(captive))
                {
                    Refuse(record, true);
                    message = $"{captive.Name} recusou ceder {offer.Target.Name}. Nova negociação em três dias.";
                    return false;
                }
                Settlement target = offer.Target;
                if (main.TryGetActiveWarBetween(captor.StringId, captive.StringId, out var war) &&
                    war.TargetSettlementId == target.StringId && war.ObjectiveAchievedOnDay < 0)
                    war.ObjectiveAchievedOnDay = CampaignTime.Now.ToDays;

                // O feudo vai para o soberano. Para dar ao heroi que
                // capturou, troque "decider" por "capturer ?? decider".
                if (target.OwnerClan?.Kingdom != captor)
                    ChangeOwnerOfSettlementAction.ApplyByKingDecision(decider, target);
                RoyalSettlement.ConfirmPossession(target);
                if (war != null) war.Outcome = war.AttackerKingdomId == captor.StringId ? "VICTORY" : "DEFENDER_VICTORY";
                EndCaptivityAction.ApplyByPeace(king, decider);
                MakePeaceAction.Apply(captor, captive);

                Resolve(record, "OBJECTIVE");

                message = $"{captor.Name} assegurou {target.Name}. {king.Name} foi libertado e a guerra terminou.";
                return true;
            }

            // [NOVA OFERTA 3/5] Cada oferta nova ganha um bloco "if (type ==
            // ...)" aqui, ANTES do bloco de resgate, e termina com return.
            if (type == NegotiationOfferType.Mercy)
            {
                EndCaptivityAction.ApplyByPeace(king, decider);
                MakePeaceAction.Apply(captor, captive);

                Resolve(record, "MERCY");

                message = captor.Name + " libertou " + king.Name +
                    " por clemencia; a guerra terminou.";
                return true;
            }

            // Resgate
            if (!offer.WillAccept && !IsPlayerSovereignOf(captive))
            {
                Refuse(record, false);
                message = captive.Name + " recusou pagar " + offer.Amount +
                    " denarios por " + king.Name + ". Tente outra oferta.";
                return false;
            }

            main.TryGetActiveWarBetween(captor.StringId, captive.StringId, out var ransomWar);
            int paid = RoyalSettlement.Pay(captive, captor, offer.Amount, GetValidCapturer(record, captor), ransomWar);
            if (paid == 0) { message = "Resgate indisponível."; return false; }

            AuthorizedRansom = king;
            try { EndCaptivityAction.ApplyByRansom(king, decider); }
            finally { AuthorizedRansom = null; }
            if (type == NegotiationOfferType.RansomHeavy) MakePeaceAction.Apply(captor, captive);

            Resolve(record, type == NegotiationOfferType.RansomHeavy
                ? "RANSOM_HEAVY"
                : "RANSOM");

            message = captive.Name + " pagou " + paid + " denarios pelo resgate de " +
                king.Name + (type == NegotiationOfferType.RansomHeavy ? "; a guerra terminou." : "; a guerra continua.");
            return true;
        }

        private static void Refuse(KingCaptureRecord record, bool land)
        {
            if (land) record.Refusals++;
            record.NextAttempt = CampaignTime.Now.ToDays + 3;
        }

    }
}
