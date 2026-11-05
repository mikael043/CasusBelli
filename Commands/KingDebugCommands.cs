using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.GameMenus;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Library;

namespace CasusBelli
{
    // Comandos de teste da captura do rei, dos perfis e da nota de paz.
    public static class KingDebugCommands
    {
        // Mostra as fichas de captura e o perfil cultural de cada reino.
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "captures", "casus_belli")]
        public static string Captures(List<string> args)
        {
            CasusBelliKingBehavior kings;
            string message;

            if (!TryPrepare(out kings, out message))
            {
                return message;
            }

            StringBuilder result = new StringBuilder();
            int count = 0;

            foreach (KingCaptureRecord record in kings.AllCaptures)
            {
                count++;
                result.Append(record.KingHeroId + " (" + record.KingKingdomId +
                    ") capturado por " + record.CaptorKingdomId +
                    " no dia " +
                    record.CapturedOnDay.ToString("0") +
                    (record.IsResolved
                        ? " | resolvido: " + record.Resolution
                        : " | EM ABERTO") + "\n");
            }

            return count == 0
                ? "CB: nenhuma captura de soberano registrada."
                : "CB: capturas: " + count + "\n" + result;
        }

        // Abre a janela de negociacao do jogador (se houver uma pendente).
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "negotiate", "casus_belli")]
        public static string Negotiate(List<string> args)
        {
            CasusBelliKingBehavior kings;
            string message;

            if (!TryPrepare(out kings, out message))
            {
                return message;
            }

            KingCaptureRecord record;

            if (!kings.TryGetPlayerNegotiation(out record))
            {
                return "CB: nenhum soberano capturado aguardando negociacao.";
            }

            GameMenu.ActivateGameMenu(CasusBelliMenuBehavior.NegotiationMenuId);
            return "CB: abrindo a janela de negociacao.";
        }

        // Cheat de teste: a sua party captura o soberano do reino indicado
        // (precisa estar em guerra com ele).
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "capture_king", "casus_belli")]
        public static string CaptureKing(List<string> args)
        {
            CasusBelliKingBehavior kings;
            string message;

            if (!TryPrepare(out kings, out message))
            {
                return message;
            }

            if (args == null || args.Count != 1)
            {
                return "Uso: casus_belli.capture_king <id_do_reino>";
            }

            Kingdom kingdom = FindKingdom(args[0]);

            if (kingdom == null || kingdom.Leader == null)
            {
                return "CB: reino ou soberano nao encontrado.";
            }

            Kingdom own = Clan.PlayerClan != null
                ? Clan.PlayerClan.Kingdom
                : null;

            if (own == null || !own.IsAtWarWith(kingdom))
            {
                return "CB: o seu reino precisa estar em guerra com " +
                    args[0] + ".";
            }

            TakePrisonerAction.Apply(MobileParty.MainParty.Party, kingdom.Leader);

            return "CB: " + kingdom.Leader.Name + " capturado. Em ate 1 hora " +
                "de jogo a ficha de captura e criada.";
        }

        // Resolve a negociacao do jogador: objective, moderate ou heavy.
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "resolve_capture", "casus_belli")]
        public static string ResolveCapture(List<string> args)
        {
            CasusBelliKingBehavior kings;
            string message;

            if (!TryPrepare(out kings, out message))
            {
                return message;
            }

            if (args == null || args.Count != 1)
            {
                return "Uso: casus_belli.resolve_capture " +
                    "<objective|moderate|heavy>";
            }

            NegotiationOfferType type;

            if (args[0] == "objective")
            {
                type = NegotiationOfferType.Objective;
            }
            else if (args[0] == "moderate")
            {
                type = NegotiationOfferType.RansomModerate;
            }
            else if (args[0] == "heavy")
            {
                type = NegotiationOfferType.RansomHeavy;
            }
            else
            {
                return "CB: opção invalida: " + args[0];
            }

            KingCaptureRecord record;

            if (!kings.TryGetPlayerNegotiation(out record))
            {
                return "CB: nenhum soberano capturado aguardando negociação.";
            }

            string result;
            bool done = kings.ExecuteOffer(record, type, out result);

            return (done ? "CB: " : "CB: falhou - ") + result;
        }

        // Perfil cultural de um reino.
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "profile", "casus_belli")]
        public static string Profile(List<string> args)
        {
            CasusBelliKingBehavior kings;
            string message;

            if (!TryPrepare(out kings, out message))
            {
                return message;
            }

            if (args == null || args.Count != 1)
            {
                return "Uso: casus_belli.profile <id_do_reino>";
            }

            KingdomProfile p = KingdomProfiles.Get(args[0]);

            return "CB: " + args[0] + " | " + p.Name + " | " + p.Description +
                "\n  guerra: com claim x" + p.WithClaimFactor.ToString("0.##") +
                ", sem claim x" + p.WithoutClaimFactor.ToString("0.##") +
                ", tregua x" + p.TruceFactor.ToString("0.##") +
                "\n  IA: guerra x" + p.AiWarChanceMultiplier.ToString("0.##") +
                ", fabricacao x" + p.AiFabricationMultiplier.ToString("0.##") +
                "\n  paz x" + p.PeaceEagerness.ToString("0.##") +
                ", vinganca x" + p.Vengefulness.ToString("0.##") +
                "\n  resgate: paga x" + p.RansomPayFactor.ToString("0.##") +
                ", exige x" + p.RansomDemandFactor.ToString("0.##") +
                ", prefere objetivo: " + p.PrefersObjectiveOverRansom;
        }

        // Nota de paz da IA do jogo base ("A declara paz a B"), ja com o
        // fator do mod. Maior = mais vontade de paz.
        [CommandLineFunctionality.CommandLineArgumentFunction(
            "peace_score", "casus_belli")]
        public static string PeaceScore(List<string> args)
        {
            CasusBelliKingBehavior kings;
            string message;

            if (!TryPrepare(out kings, out message))
            {
                return message;
            }

            if (args == null || args.Count != 2)
            {
                return "Uso: casus_belli.peace_score <reino_A> <reino_B>";
            }

            Kingdom a = FindKingdom(args[0]);
            Kingdom b = FindKingdom(args[1]);

            if (a == null || b == null)
            {
                return "CB: reino nao encontrado.";
            }

            float score =
                Campaign.Current.Models.DiplomacyModel.GetScoreOfDeclaringPeace(
                    a,
                    b);

            return "CB: nota de paz " + a.StringId + " -> " + b.StringId +
                ": " + score.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static bool TryPrepare(
            out CasusBelliKingBehavior kings,
            out string message)
        {
            kings = null;
            message = "";

            string error = "";

            if (!CampaignCheats.CheckCheatUsage(ref error))
            {
                message = error;
                return false;
            }

            if (Campaign.Current == null)
            {
                message = "CB: nenhuma campanha ativa.";
                return false;
            }

            kings = Campaign.Current.GetCampaignBehavior<CasusBelliKingBehavior>();

            if (kings == null)
            {
                message = "CB: behavior do rei não encontrado.";
                return false;
            }

            return true;
        }

        private static Kingdom FindKingdom(string kingdomId)
        {
            foreach (Kingdom kingdom in Kingdom.All)
            {
                if (kingdom.StringId == kingdomId)
                {
                    return kingdom;
                }
            }

            return null;
        }
    }
}
