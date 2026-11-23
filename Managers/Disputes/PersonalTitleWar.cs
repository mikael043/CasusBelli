using System;
using System.Linq;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli.Disputes
{
    internal static class PersonalTitleWar
    {
        internal const int DemandCost = 40;
        internal const int ReplyDays = 7;
        internal const int RoyalInterventionCost = 30;
        internal const int RoyalInterventionCooldownDays = 84;
        internal const int RoyalInterventionAiMinDays = 7;
        internal const int RoyalInterventionAiMinBalance = 40;
        internal const int RoyalInterventionAiReserve = 60;

        // Only an actual war against another kingdom justifies emergency royal intervention.
        // Permanent bandit hostility is not a kingdom war.
        internal static bool AutoInterventionEligible(bool playerSovereign, bool royalAuthority, bool externalKingdomWar) =>
            !playerSovereign && royalAuthority && externalKingdomWar;

        internal static bool RoyalInterventionRules(bool active, bool atWar, bool crownParty, bool crownBacked,
            int authority, float influence, int day, int nextAllowedDay) =>
            active && atWar && !crownParty && !crownBacked && authority >= 60 &&
            influence >= RoyalInterventionCost && day >= nextAllowedDay;

        internal static bool AutoInterventionReady(bool playerSovereign, bool permitted, bool externalKingdomWar,
            int elapsedWarDays, float absoluteBalance, float crownInfluence) =>
            AutoInterventionEligible(playerSovereign, permitted, externalKingdomWar) &&
            elapsedWarDays >= RoyalInterventionAiMinDays &&
            absoluteBalance >= RoyalInterventionAiMinBalance &&
            crownInfluence >= RoyalInterventionCost + RoyalInterventionAiReserve;

        internal static int RoyalInterventionNextDay(Kingdom kingdom) =>
            kingdom == null ? 0 : RealmPolitics.Current?.Number("royal_intervention_next|" + kingdom.StringId, 0) ?? 0;

        internal static string InterventionBlock(TitleDispute d)
        {
            if (d == null || !d.IsActive) return "Não há disputa válida.";
            if (!d.Demanded || !d.Contested) return "A Coroa só pode impor paz após começar a guerra pessoal.";
            if (d.Realm?.RulingClan == null || d.Realm.Leader == null) return "Este reino não possui soberano apto.";
            if (d.Realm.Leader == d.Claimant || d.Realm.Leader == d.Holder)
                return "O soberano é parte da disputa e não pode agir como mediador neutro.";
            if (d.Offers.Any(o => o.Accepted && o.Recipient == d.Realm.RulingClan))
                return "A Coroa já declarou apoio a um dos lados.";
            if (RealmPolitics.Authority(d.Realm) < 60)
                return "A autoridade real precisa ser de pelo menos 60.";
            if (d.Realm.RulingClan.Influence < RoyalInterventionCost)
                return $"A intervenção custa {RoyalInterventionCost} de influência.";
            int wait = RoyalInterventionNextDay(d.Realm) - (int)CampaignTime.Now.ToDays;
            if (wait > 0) return $"A Coroa só poderá intervir novamente neste reino em {wait} dias.";
            return RoyalInterventionRules(d.IsActive, d.Demanded && d.Contested,
                d.Realm.Leader == d.Claimant || d.Realm.Leader == d.Holder,
                d.Offers.Any(o => o.Accepted && o.Recipient == d.Realm.RulingClan),
                RealmPolitics.Authority(d.Realm), d.Realm.RulingClan.Influence,
                (int)CampaignTime.Now.ToDays, RoyalInterventionNextDay(d.Realm))
                ? null : "A Coroa não atende às condições políticas para intervir.";
        }

        internal static bool HasPoliticalMajority(int supporters, int peers) =>
            peers > 0 && supporters >= 0 && supporters <= peers && (long)supporters * 2 > peers;

        internal static int DucalPeers(TitleDispute d) => d.Realm.Clans.Count(d.Eligible);
        internal static int DucalSupporters(TitleDispute d) => d.Offers.Where(o => o.Accepted && o.ForClaimant && d.Eligible(o.Recipient) && !TitleDisputeBehavior.DucalCommitmentBlocked(o.Recipient, d) &&
            DisputeAi.EvaluateDucalSupport(d.Title, d.Claimant, o.Recipient, out _)).Select(o => o.Recipient).Distinct().Count();

        internal static string DemandBlock(TitleDispute dispute)
        {
            if (!dispute.CanDemand) return "A reivindicação precisa estar madura.";
            if (RealmPolitics.Authority(dispute.Realm) >= 75 && dispute.Claimant != dispute.Realm.Leader)
                return "A autoridade da Coroa (75 ou mais) proíbe ultimatos privados.";
            if (dispute.TruceEnds.IsFuture) return $"Trégua por mais {System.Math.Ceiling(dispute.TruceEnds.RemainingDaysFromNow)} dias.";
            if (TitleDisputeBehavior.DucalCommitmentBlocked(dispute.Claimant.Clan, dispute) ||
                TitleDisputeBehavior.DucalCommitmentBlocked(dispute.Holder.Clan, dispute))
                return "Um dos clãs está comprometido em outra disputa.";
            if (dispute.IsDucal)
            {
                var block = DisputeAi.DucalSponsorBlock(dispute.Title, dispute.Claimant);
                if (block != null) return block;
                int peers = DucalPeers(dispute), supporters = DucalSupporters(dispute);
                if (!HasPoliticalMajority(supporters, peers))
                    return $"É necessária a maioria absoluta dos outros pares plenos ({supporters}/{peers} apoios válidos).";
                if (Troops(dispute, true) <= 0 || Troops(dispute, true) < Troops(dispute, false))
                    return "A coalizão precisa ter pelo menos a força militar do titular e seus aliados.";
            }
            else if (!dispute.Offers.Any(o => o.Accepted && o.ForClaimant && dispute.Eligible(o.Recipient)))
                return "É preciso o apoio confirmado de outro clã do reino.";
            if (dispute.Claimant.Clan.Influence < DemandCost)
                return $"O ultimato custa {DemandCost} de influência.";
            var active = Campaign.Current.GetCampaignBehavior<TitleDisputeBehavior>().Active;
            if (active.Any(other => other.Demanded &&
                (other.Claimant.Clan == dispute.Claimant.Clan || other.Holder.Clan == dispute.Claimant.Clan ||
                 other.Claimant.Clan == dispute.Holder.Clan || other.Holder.Clan == dispute.Holder.Clan)))
                return "Um dos dois clãs já é parte principal em outro ultimato ou guerra pessoal.";
            return null;
        }

        internal static void Demand(TitleDispute dispute)
        {
            if (DemandBlock(dispute) != null) return;
            ChangeClanInfluenceAction.Apply(dispute.Claimant.Clan, -DemandCost);
            if (dispute.IsDucal)
                dispute.Offers.RemoveAll(o => o.Accepted && (!dispute.Eligible(o.Recipient) || TitleDisputeBehavior.DucalCommitmentBlocked(o.Recipient, dispute) ||
                    (o.ForClaimant && !DisputeAi.EvaluateDucalSupport(dispute.Title, dispute.Claimant, o.Recipient, out _))));
            dispute.ReplyDeadline = CampaignTime.DaysFromNow(ReplyDays);
            dispute.Demanded = true;
            Report(dispute, "O pretendente apresentou um ultimato ao titular.");
        }

        internal static void Reply(TitleDispute dispute, bool yieldTitle)
        {
            if (!dispute.IsActive || !dispute.Demanded || dispute.Contested) return;
            if (yieldTitle) Resolve(dispute, true);
            else
            {
                var envoy = CouncilDuties.Officer(dispute.Realm,CouncilOffice.Chancellor);
                if (!dispute.MediationAttempted && envoy != null && envoy != dispute.Claimant && envoy != dispute.Holder)
                {
                    dispute.MediationAttempted = true;
                    int trust = Math.Min(envoy.GetRelation(dispute.Claimant), envoy.GetRelation(dispute.Holder));
                    if (MBRandom.RandomFloat < Math.Max(0,Math.Min(.8f,RealmPolitics.Authority(dispute.Realm) / 300f + envoy.GetSkillValue(DefaultSkills.Charm)/500f + trust/300f)))
                    {
                        void Mediate()
                        {
                            if (!dispute.IsActive || !dispute.Demanded || dispute.Contested) return;
                            dispute.Demanded = false;
                            dispute.TruceEnds = CampaignTime.DaysFromNow(30);
                            Report(dispute,$"{envoy.Name} mediou uma trégua de 30 dias. O titular conserva o título e o pretendente mantém sua reivindicação.");
                        }
                        if (dispute.Realm.Leader == Hero.MainHero || dispute.Claimant.Clan == Clan.PlayerClan || dispute.Holder.Clan == Clan.PlayerClan)
                            InformationManager.ShowInquiry(new InquiryData("Mediação do chanceler", $"{envoy.Name} propõe suspender por 30 dias a disputa por {dispute.Title.FullName}. Aceitar?",
                                true,true,"Aceitar","Recusar",Mediate,() => BeginConflict(dispute)));
                        else Mediate();
                        return;
                    }
                }
                BeginConflict(dispute);
            }
        }

        internal static bool CanIntervene(TitleDispute d) => InterventionBlock(d) == null;
        internal static void Intervene(TitleDispute d)
        {
            if (!CanIntervene(d)) return;
            var realm = d.Realm;
            ChangeClanInfluenceAction.Apply(realm.RulingClan, -RoyalInterventionCost);
            d.Contested = d.Demanded = false;
            d.PeaceProposer = null;
            d.Offers.Clear();
            d.Balance = 0;
            d.TruceEnds = CampaignTime.DaysFromNow(60);
            d.WarStartedAt = default(CampaignTime);
            RealmPolitics.Current?.Write("royal_intervention_next|" + realm.StringId,
                ((int)CampaignTime.Now.ToDays + RoyalInterventionCooldownDays).ToString());
            string message = $"O soberano impôs 60 dias de paz, gastando {RoyalInterventionCost} de influência. " +
                "O titular conserva o título e a reivindicação permanece válida.";
            Report(d, message);
            if (realm.Leader == Hero.MainHero && d.Claimant.Clan != Clan.PlayerClan &&
                d.Holder.Clan != Clan.PlayerClan)
                InformationManager.DisplayMessage(new InformationMessage($"{d.Title.FullName}: {message}"));
        }

        private static void BeginConflict(TitleDispute dispute)
        {
            if (!dispute.IsActive || !dispute.Demanded || dispute.Contested) return;
            dispute.Contested = true;
            dispute.WarStartedAt = CampaignTime.Now;
            dispute.NextPeaceOffer = CampaignTime.DaysFromNow(7);
            Report(dispute, "O titular recusou. Começou a guerra pessoal.");
            if (dispute.Realm.Leader == Hero.MainHero && dispute.Claimant.Clan != Clan.PlayerClan &&
                dispute.Holder.Clan != Clan.PlayerClan)
                InformationManager.DisplayMessage(new InformationMessage(
                    $"{dispute.Title.FullName}: começou uma guerra pessoal entre {dispute.Claimant.Name} " +
                    $"e {dispute.Holder.Name}. A Coroa pode avaliar uma intervenção em K → CasusBelli → Disputas internas."));
        }

        internal static int Troops(TitleDispute dispute, bool attacker)
        {
            var clans = dispute.Offers.Where(o => o.Accepted && o.ForClaimant == attacker &&
                dispute.Eligible(o.Recipient) && (!dispute.IsDucal || dispute.Demanded ||
                (!TitleDisputeBehavior.DucalCommitmentBlocked(o.Recipient, dispute) &&
                 (!attacker || DisputeAi.EvaluateDucalSupport(dispute.Title, dispute.Claimant, o.Recipient, out _))))).Select(o => o.Recipient)
                .Concat(new[] { dispute.Sponsor(attacker).Clan }).Distinct();
            return clans.Sum(DisputeAi.FieldTroops);
        }

        internal static void Tick(TitleDispute dispute)
        {
            if (!dispute.Contested)
            {
                // Give pending ultimatums from older saves a full response period.
                if (dispute.ReplyDeadline.Equals(default(CampaignTime)))
                    dispute.ReplyDeadline = CampaignTime.DaysFromNow(ReplyDays);
                if (!dispute.ReplyDeadline.IsFuture)
                {
                    Reply(dispute, false);
                    return;
                }
                if (dispute.Holder.Clan == Clan.PlayerClan) return;
            }
            int attack = Troops(dispute, true);
            int defense = Troops(dispute, false);
            if (!dispute.Contested)
            {
                Reply(dispute, attack > 0 && DisputeAi.Yield(dispute, DisputeAi.Advantage(attack, defense)));
                return;
            }
            // Old saves have no recorded war start. Begin counting rather than granting instant AI intervention.
            if (dispute.WarStartedAt.Equals(default(CampaignTime)))
                dispute.WarStartedAt = CampaignTime.Now;
            int warDays = Math.Max(0, (int)(CampaignTime.Now.ToDays - dispute.WarStartedAt.ToDays));
            if (AutoInterventionReady(dispute.Realm.Leader == Hero.MainHero, CanIntervene(dispute),
                DisputeAi.HasExternalKingdomWar(dispute.Realm), warDays, Math.Abs(dispute.Balance),
                dispute.Realm.RulingClan?.Influence ?? 0f) && MBRandom.RandomFloat < .08f)
            { Intervene(dispute); return; }
            PersonalPeace.Tick(dispute);
            if (!dispute.Contested || !dispute.IsActive) return;
            // One abstract engagement per day, weighted by the pledged clans' field troops.
            float odds = (attack + 1f) / (attack + defense + 2f);
            dispute.Balance += MBRandom.RandomFloat < odds ? 20 : -20;
            if (System.Math.Abs(dispute.Balance) >= 100) Resolve(dispute, dispute.Balance > 0);
        }

        internal static bool TransferDucalTitle(TitleDispute dispute)
        {
            if (!dispute.IsDucal || !dispute.IsActive) return false;
            // Change the legal title only. Do not recurse into its vassals or settlements.
            TitleManager.Instance.InheritTitle(dispute.Holder, dispute.Claimant, dispute.Title);
            if (dispute.Title.deJure != dispute.Claimant) return false;
            dispute.Title.AddClaim(dispute.Holder, ClaimType.PreviousOwner, true);
            dispute.Title.RemoveClaim(dispute.Claimant);
            dispute.Finished = true;
            return true;
        }

        internal static void Resolve(TitleDispute dispute, bool won)
        {
            if (!dispute.IsActive) return;
            if (won)
            {
                if (dispute.IsDucal)
                {
                    if (!TransferDucalTitle(dispute)) return;
                }
                else
                {
                    ChangeOwnerOfSettlementAction.ApplyByDefault(dispute.Claimant, dispute.Title.Fief);
                    TitleManager.Instance.InheritTitle(dispute.Holder, dispute.Claimant, dispute.Title);
                }
            }
            dispute.Title.RemoveClaim(dispute.Claimant);
            dispute.Finished = true;
            Report(dispute, won ? (dispute.IsDucal ? "O pretendente recebeu o ducado; os feudos subordinados permanecem com seus titulares." :
                "O pretendente recebeu o título e o feudo.") : "O titular derrotou a reivindicação.");
        }

        internal static void Report(TitleDispute dispute, string message)
        {
            RealmPolitics.Current?.Write("personal_dispute_recent|" + dispute.Realm.StringId,
                $"Dia {(int)CampaignTime.Now.ToDays} · {dispute.Title.FullName}: {message}");
            if (dispute.Claimant.Clan == Clan.PlayerClan || dispute.Holder.Clan == Clan.PlayerClan ||
                dispute.Offers.Any(o => o.Accepted && o.Recipient == Clan.PlayerClan))
                InformationManager.DisplayMessage(new InformationMessage($"{dispute.Title.FullName}: {message}"));
        }
    }
}