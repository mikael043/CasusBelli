using System;
using System.Collections.Generic;
using System.Linq;
using CasusBelli.Disputes;
using CasusBelli.Managers.Titles;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli.UI.Titles
{
    internal static class DisputeMenu
    {
        // TODO release: remove the diagnostic breakdown (keep only qualitative feedback if useful).
        internal static void PreviewDucalSupport(FeudalTitle title, Hero claimant, Action back)
        {
            string block = DisputeAi.DucalSponsorBlock(title, claimant);
            if (block != null)
            {
                InformationManager.ShowInquiry(new InquiryData("Apoio político", block, true, false, "Voltar", "", back, null));
                return;
            }
            var realm = claimant.Clan.Kingdom;
            var forecasts = realm.Clans.Where(c => c != claimant.Clan && c != title.deJure.Clan && c.Leader != null
                && RealmPeers.Rank(c) == 2).Select(c =>
                {
                    bool accepts = DisputeAi.EvaluateDucalSupport(title, claimant, c, out string reason);
                    return new { Clan = c, Accepts = accepts, Reason = reason };
                }).OrderByDescending(f => f.Accepts).ThenBy(f => f.Clan.Name.ToString()).ToList();
            if (forecasts.Count == 0)
            {
                InformationManager.ShowInquiry(new InquiryData("Apoio político", "Não há outros pares plenos elegíveis.",
                    true, false, "Voltar", "", back, null));
                return;
            }
            var options = forecasts.Select(f => new InquiryElement(f.Clan,
                $"{f.Clan.Name} — {(f.Accepts ? "favorável" : "não apoiaria")}", null, true, f.Reason)).ToList();
            Select("Apoio político — " + title.FullName,
                $"Previsão experimental: {forecasts.Count(f => f.Accepts)}/{forecasts.Count} pares plenos favoráveis. Nenhum compromisso é assumido. Selecione um clã para conferir os motivos.",
                options, selected =>
                {
                    var f = forecasts.First(x => x.Clan == (Clan)selected);
                    InformationManager.ShowInquiry(new InquiryData(f.Clan.Name.ToString(),
                        f.Reason, true, false, "Voltar", "", () => PreviewDucalSupport(title, claimant, back), null));
                }, back);
        }

        internal static void OpenDucal(FeudalTitle title, Action back)
        {
            var behavior = Campaign.Current.GetCampaignBehavior<TitleDisputeBehavior>();
            var existing = behavior.Active.FirstOrDefault(d => d.Title == title && d.Claimant == Hero.MainHero);
            if (existing != null) { Details(existing, back); return; }
            var block = behavior.DucalOpeningBlock(title, Hero.MainHero);
            if (block != null)
            {
                InformationManager.ShowInquiry(new InquiryData("Disputa ducal", block, true, false, "Voltar", "", back, null));
                return;
            }
            InformationManager.ShowInquiry(new InquiryData("Iniciar disputa ducal",
                "Recrute a maioria absoluta dos outros pares plenos e forme uma coalizão militar capaz de enfrentar o titular. " +
                "A vitória transfere apenas o ducado; os condados e baronias continuam com seus titulares.",
                true, true, "Iniciar", "Cancelar", () =>
                {
                    var dispute = behavior.OpenDucal(title, Hero.MainHero);
                    if (dispute != null) Details(dispute, back); else back();
                }, back));
        }

        internal static void Open(Kingdom kingdom, Action back)
        {
            var behavior = Campaign.Current.GetCampaignBehavior<TitleDisputeBehavior>();
            var choices = behavior.Active.Where(d => d.Realm == kingdom)
                .Select(d => new InquiryElement(d, $"{d.Title.FullName} — {d.Claimant.Name}", null)).ToList();
            if (choices.Count == 0)
            {
                InformationManager.ShowInquiry(new InquiryData("Disputas internas",
                    "Nenhuma disputa interna ativa neste reino.",
                    true, false, "Voltar", "", back, null));
                return;
            }
            Select("Disputas internas", "Escolha uma disputa para consultar os apoios ou enviar um emissário.",
                choices, item => Details((TitleDispute)item, () => Open(kingdom, back)), back);
        }

        private static void Details(TitleDispute dispute, Action back)
        {
            if (!dispute.IsActive) { back(); return; }
            var choices = new List<InquiryElement>();
            var player = Clan.PlayerClan;
            bool attacking = dispute.Claimant.Clan == player;
            string text = $"Pretendente: {dispute.Claimant.Name}\nTitular: {dispute.Holder.Name}";
            string asset = dispute.IsDucal ? "ducado" : "título e feudo";
            if (dispute.IsDucal && !dispute.Demanded)
                text += $"\nApoios políticos válidos: {PersonalTitleWar.DucalSupporters(dispute)}/{PersonalTitleWar.DucalPeers(dispute)} (maioria absoluta)." +
                    $"\nTropas da coalizão: {PersonalTitleWar.Troops(dispute, true)} contra {PersonalTitleWar.Troops(dispute, false)}.";
            foreach (bool side in new[] { true, false })
            {
                string supporters = string.Join(", ", dispute.Offers.Where(o => o.Accepted && o.ForClaimant == side &&
                    dispute.Eligible(o.Recipient)).Select(o => o.Recipient.Name.ToString()));
                text += $"\nApoio ao {(side ? "pretendente" : "titular")}: {(supporters.Length == 0 ? "nenhum" : supporters)}";
            }
            if (dispute.Contested)
            {
                text += $"\nGuerra pessoal em andamento. Saldo do pretendente: {dispute.Balance:0}/100 (derrota em -100).";
                if (attacking || dispute.Holder.Clan == player)
                {
                    choices.Add(new InquiryElement("surrender", attacking ? "Render-se e perder a reivindicação" : "Render-se e ceder o " + asset, null));
                    text += "\nPaz branca: o titular conserva o título, a reivindicação permanece e há trégua de 60 dias.";
                    if (dispute.PeaceProposer != null && dispute.PeaceProposer != player)
                    {
                        choices.Add(new InquiryElement("peace", "Aceitar paz branca", null));
                        choices.Add(new InquiryElement("refuse", "Recusar paz branca", null));
                    }
                    else if (PersonalPeace.CanOffer(dispute))
                        choices.Add(new InquiryElement("offer", "Propor paz branca", null));
                }
            }
            else if (dispute.Demanded)
            {
                var days = dispute.ReplyDeadline.Equals(default(CampaignTime)) ? PersonalTitleWar.ReplyDays :
                    Math.Max(0, Math.Ceiling(dispute.ReplyDeadline.RemainingDaysFromNow));
                text += $"\nUltimato entregue. Prazo restante: {days} dias. O silêncio conta como recusa.";
                if (dispute.Holder.Clan == player)
                {
                    choices.Add(new InquiryElement("yield", "Ceder " + asset, null));
                    choices.Add(new InquiryElement("resist", "Resistir em guerra pessoal", null));
                }
            }
            else if (attacking || dispute.Holder.Clan == player)
            {
                if (attacking)
                {
                    if (dispute.IsDucal)
                        choices.Add(new InquiryElement("abandon", "Abandonar a disputa e perder a reivindicação", null));
                    var reason = PersonalTitleWar.DemandBlock(dispute);
                    if (reason == null)
                        choices.Add(new InquiryElement("demand", $"Exigir título ({PersonalTitleWar.DemandCost} de influência) e encerrar recrutamento", null));
                    else text += "\nUltimato indisponível: " + reason;
                }
                text += "\nUm envio por semana. A viagem leva três dias.";
                foreach (var offer in dispute.Offers.Where(o => o.ForClaimant == attacking && !o.Accepted))
                    text += $"\n{offer.Recipient.Name}: {(offer.Answered ? "recusou" : "aguardando resposta")}";
                foreach (var clan in dispute.Realm.Clans.Where(c => dispute.CanSend(c, attacking)))
                    choices.Add(new InquiryElement(clan, "Enviar emissário a " + clan.Name, null));
            }
            else
            {
                foreach (var offer in dispute.Offers.Where(o => o.Recipient == player && dispute.CanAnswer(o)))
                {
                    var sponsor = dispute.Sponsor(offer.ForClaimant);
                    choices.Add(new InquiryElement(offer, "Responder a " + sponsor.Name, null));
                }
            }
            if (Hero.MainHero == dispute.Realm.Leader && dispute.Demanded)
            {
                string crownBlock = PersonalTitleWar.InterventionBlock(dispute);
                if (crownBlock == null)
                    choices.Add(new InquiryElement("intervene",
                        $"Impor paz real por 60 dias · {PersonalTitleWar.RoyalInterventionCost} de influência", null));
                else text += "\nIntervenção da Coroa indisponível: " + crownBlock;
            }
            if (choices.Count == 0)
            {
                InformationManager.ShowInquiry(new InquiryData(dispute.Title.FullName.ToString(), text,
                    true, false, "Voltar", "", back, null));
                return;
            }
            Select(dispute.Title.FullName.ToString(), text, choices, item =>
            {
                if (item is string command)
                {
                    if (command == "intervene")
                    {
                        InformationManager.ShowInquiry(new InquiryData("Intervenção soberana",
                            "Impor uma trégua de 60 dias aos dois clãs? O título e a reivindicação serão preservados. " +
                            $"Custo: {PersonalTitleWar.RoyalInterventionCost} de influência. " +
                            $"Uma nova intervenção neste reino ficará bloqueada por {PersonalTitleWar.RoyalInterventionCooldownDays} dias.",
                            true, true, "Impor paz", "Cancelar",
                            () => { PersonalTitleWar.Intervene(dispute); Details(dispute, back); },
                            () => Details(dispute, back)));
                        return;
                    }
                    else if (command == "demand") PersonalTitleWar.Demand(dispute);
                    else if (command == "abandon")
                    {
                        InformationManager.ShowInquiry(new InquiryData("Abandonar disputa", "A reivindicação ducal será perdida.",
                            true, true, "Abandonar", "Cancelar", () =>
                            {
                                if (dispute.IsActive && !dispute.Demanded && dispute.Claimant.Clan == player)
                                {
                                    dispute.Title.RemoveClaim(dispute.Claimant);
                                    dispute.Finished = true;
                                }
                                back();
                            }, () => Details(dispute, back)));
                        return;
                    }
                    else if (command == "surrender")
                    {
                        InformationManager.ShowInquiry(new InquiryData("Confirmar rendição", attacking ?
                            "Seu clã perderá a reivindicação." : "O pretendente receberá o " + asset + ".",
                            true, true, "Render-se", "Cancelar", () => { PersonalPeace.Surrender(dispute, player); Details(dispute, back); },
                            () => Details(dispute, back)));
                        return;
                    }
                    else if (command == "offer") PersonalPeace.Offer(dispute, player);
                    else if (command == "peace" || command == "refuse") PersonalPeace.Answer(dispute, player, command == "peace");
                    else PersonalTitleWar.Reply(dispute, command == "yield");
                    Details(dispute, back);
                    return;
                }
                if (item is Clan clan) { dispute.Send(clan, attacking); Details(dispute, back); return; }
                var offer = (SupportOffer)item;
                if (dispute.IsDucal && offer.ForClaimant && !DisputeAi.EvaluateDucalSupport(dispute.Title, dispute.Claimant, player, out string reason))
                {
                    InformationManager.ShowInquiry(new InquiryData("Pedido de apoio", "A proposta não atende aos critérios políticos.\n" + reason,
                        true, true, "Recusar", "Voltar", () => { if (dispute.CanAnswer(offer)) offer.Answer(false); Details(dispute, back); },
                        () => Details(dispute, back)));
                    return;
                }
                InformationManager.ShowInquiry(new InquiryData("Pedido de apoio",
                    $"Comprometer seu clã com {dispute.Sponsor(offer.ForClaimant).Name} na disputa por {dispute.Title.FullName}?",
                    true, true, "Apoiar", "Recusar",
                    () => { if (dispute.CanAnswer(offer) && (!dispute.IsDucal || !offer.ForClaimant || DisputeAi.EvaluateDucalSupport(dispute.Title, dispute.Claimant, player, out _))) offer.Answer(true); Details(dispute, back); },
                    () => { if (dispute.CanAnswer(offer)) offer.Answer(false); Details(dispute, back); }));
            }, back);
        }

        private static void Select(string heading, string text, List<InquiryElement> items, Action<object> choose, Action back)
        {
            MBInformationManager.ShowMultiSelectionInquiry(new MultiSelectionInquiryData(heading, text, items,
                true, 1, 1, "Selecionar", "Voltar", selected =>
                {
                    if (selected.Count == 1) choose(selected[0].Identifier);
                }, selected => back()));
        }
    }
}