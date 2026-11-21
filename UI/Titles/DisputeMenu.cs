using System;
using System.Collections.Generic;
using System.Linq;
using CasusBelli.Disputes;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace CasusBelli.UI.Titles
{
    internal static class DisputeMenu
    {
        internal static void Open(Kingdom kingdom, Action back)
        {
            var behavior = Campaign.Current.GetCampaignBehavior<TitleDisputeBehavior>();
            var choices = behavior.Active.Where(d => d.Realm == kingdom)
                .Select(d => new InquiryElement(d, $"{d.Title.FullName} — {d.Claimant.Name}", null)).ToList();
            if (choices.Count == 0)
            {
                InformationManager.ShowInquiry(new InquiryData("Disputas internas",
                    "Nenhuma reivindicação entre clãs deste reino por um condado ou baronia controlado pelo titular.",
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
                    choices.Add(new InquiryElement("surrender", attacking ? "Render-se e perder a reivindicação" : "Render-se e ceder o título e o feudo", null));
                    text += "\nPaz branca: o titular conserva o feudo, a reivindicação permanece e há trégua de 60 dias.";
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
                    choices.Add(new InquiryElement("yield", "Ceder título e feudo", null));
                    choices.Add(new InquiryElement("resist", "Resistir em guerra pessoal", null));
                }
            }
            else if (attacking || dispute.Holder.Clan == player)
            {
                if (attacking)
                {
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
            if (Hero.MainHero == dispute.Realm.Leader && PersonalTitleWar.CanIntervene(dispute))
                choices.Add(new InquiryElement("intervene","Impor paz por 60 dias · 30 de influência",null));
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
                    if (command == "intervene") PersonalTitleWar.Intervene(dispute);
                    else if (command == "demand") PersonalTitleWar.Demand(dispute);
                    else if (command == "surrender")
                    {
                        InformationManager.ShowInquiry(new InquiryData("Confirmar rendição", attacking ?
                            "Seu clã perderá a reivindicação." : "O pretendente receberá o título e o feudo.",
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
                InformationManager.ShowInquiry(new InquiryData("Pedido de apoio",
                    $"Comprometer seu clã com {dispute.Sponsor(offer.ForClaimant).Name} na disputa por {dispute.Title.FullName}?",
                    true, true, "Apoiar", "Recusar",
                    () => { if (dispute.CanAnswer(offer)) offer.Answer(true); Details(dispute, back); },
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
