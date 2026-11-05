using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.CharacterDevelopment;

namespace CasusBelli
{
    // Como cada reino REAGE ao sistema de casus belli, com base na cultura
    // real que o inspira. Tudo aqui e dado: para mudar o jeito de um reino,
    // mude os numeros dele. Os valores sao pontos de partida para testar.
    public class KingdomProfile
    {
        public string Name = "";
        public string Description = "";

        // Multiplicadores da nota de guerra da IA do jogo base.
        public float WithClaimFactor = 1.5f;      // tem claim: mais vontade
        public float WithoutClaimFactor = 0.5f;   // sem claim: menos vontade
        public float TruceFactor = 0.05f;         // durante a tregua

        // Multiplicadores da IA do proprio mod.
        public float AiWarChanceMultiplier = 1f;
        public float AiFabricationMultiplier = 1f;

        // Paz e vinganca.
        public float PeaceEagerness = 1f;         // vontade de paz
        public float Vengefulness = 1f;           // desejo de revanche

        // Resgate do rei. Pay: quanto aceita pagar. Demand: quanto exige.
        public float RansomPayFactor = 1f;
        public float RansomDemandFactor = 1f;
        public bool PrefersObjectiveOverRansom = false;
    }

    public static class KingdomProfiles
    {
        private static readonly KingdomProfile DefaultProfile =
            new KingdomProfile
            {
                Name = "Reino comum",
                Description = "Sem tracos culturais especiais."
            };

        private static readonly Dictionary<string, KingdomProfile> Profiles =
            Build();

        public static KingdomProfile Get(string kingdomId)
        {
            KingdomProfile profile;

            if (!string.IsNullOrEmpty(kingdomId) &&
                Profiles.TryGetValue(kingdomId, out profile))
            {
                return profile;
            }

            return DefaultProfile;
        }

        private static Dictionary<string, KingdomProfile> Build()
        {
            Dictionary<string, KingdomProfile> map =
                new Dictionary<string, KingdomProfile>();

            // Imperio: inspirado em Roma e Bizancio. Legalista e diplomatico:
            // quer pretexto juridico, fabrica claims, paga resgate com facilidade.
            KingdomProfile empire = new KingdomProfile
            {
                Name = "Legalismo imperial",
                Description = "Quer pretexto juridico, fabrica claims e " +
                    "resolve crises com ouro e diplomacia.",
                WithClaimFactor = 1.6f,
                WithoutClaimFactor = 0.35f,
                TruceFactor = 0.03f,
                AiWarChanceMultiplier = 0.8f,
                AiFabricationMultiplier = 1.5f,
                PeaceEagerness = 1.2f,
                Vengefulness = 0.8f,
                RansomPayFactor = 1.3f,
                RansomDemandFactor = 1.0f,
                PrefersObjectiveOverRansom = false
            };
            map["empire"] = empire;
            map["empire_s"] = empire;
            map["empire_w"] = empire;

            // Vlandia: inspirada na cavalaria feudal francesa. Honra e
            // resgate de nobres sao tradicao; exige muito e paga bem.
            map["vlandia"] = new KingdomProfile
            {
                Name = "Honra feudal",
                Description = "Guerra de cavaleiros: respeita a tregua, " +
                    "valoriza o casus belli e trata resgate como direito.",
                WithClaimFactor = 1.5f,
                WithoutClaimFactor = 0.45f,
                TruceFactor = 0.04f,
                AiWarChanceMultiplier = 1.0f,
                AiFabricationMultiplier = 1.0f,
                PeaceEagerness = 1.0f,
                Vengefulness = 1.0f,
                RansomPayFactor = 1.5f,
                RansomDemandFactor = 1.3f,
                PrefersObjectiveOverRansom = false
            };

            // Sturgia: inspirada nos principados da Rus e nos nordicos.
            // Saque e butim; paz mais fraca; prefere tomar terra a ouro.
            map["sturgia"] = new KingdomProfile
            {
                Name = "Principados guerreiros",
                Description = "Guerreiros de fronteira: aceitam guerra com " +
                    "pouco pretexto e preferem terra a ouro.",
                WithClaimFactor = 1.4f,
                WithoutClaimFactor = 0.7f,
                TruceFactor = 0.08f,
                AiWarChanceMultiplier = 1.1f,
                AiFabricationMultiplier = 0.7f,
                PeaceEagerness = 0.9f,
                Vengefulness = 1.2f,
                RansomPayFactor = 0.8f,
                RansomDemandFactor = 1.2f,
                PrefersObjectiveOverRansom = true
            };

            // Battania: inspirada nos cla celtas. Faida de sangue:
            // vinganca forte, pouca disposicao a pagar para voltar.
            map["battania"] = new KingdomProfile
            {
                Name = "Faida de cla",
                Description = "Cobra sangue por sangue, guarda rancor por " +
                    "muito tempo e detesta pagar resgate.",
                WithClaimFactor = 1.5f,
                WithoutClaimFactor = 0.6f,
                TruceFactor = 0.10f,
                AiWarChanceMultiplier = 0.9f,
                AiFabricationMultiplier = 0.6f,
                PeaceEagerness = 0.8f,
                Vengefulness = 1.6f,
                RansomPayFactor = 0.6f,
                RansomDemandFactor = 0.9f,
                PrefersObjectiveOverRansom = true
            };

            // Aserai: inspirado nos califados e na rede comercial arabe.
            // Pragmatico: resgate e negocio, paz e bom para o comercio.
            map["aserai"] = new KingdomProfile
            {
                Name = "Pragmatismo mercantil",
                Description = "Resgate e negocio: paga e cobra muito, " +
                    "busca a paz quando o comercio sofre.",
                WithClaimFactor = 1.4f,
                WithoutClaimFactor = 0.55f,
                TruceFactor = 0.04f,
                AiWarChanceMultiplier = 0.9f,
                AiFabricationMultiplier = 1.2f,
                PeaceEagerness = 1.3f,
                Vengefulness = 0.7f,
                RansomPayFactor = 1.6f,
                RansomDemandFactor = 1.5f,
                PrefersObjectiveOverRansom = false
            };

            // Khuzait: inspirado nos imperios das estepes (mongois).
            // Conquista como modo de vida; quase nao precisa de pretexto.
            map["khuzait"] = new KingdomProfile
            {
                Name = "Conquista das estepes",
                Description = "Expansao constante, pouco apego a pretextos e " +
                    "ordens de tributo pesadas.",
                WithClaimFactor = 1.3f,
                WithoutClaimFactor = 0.85f,
                TruceFactor = 0.15f,
                AiWarChanceMultiplier = 1.4f,
                AiFabricationMultiplier = 0.5f,
                PeaceEagerness = 0.8f,
                Vengefulness = 1.1f,
                RansomPayFactor = 0.7f,
                RansomDemandFactor = 1.4f,
                PrefersObjectiveOverRansom = true
            };

            return map;
        }
    }

    // Temperamento do rei pelos tracos do heroi. Valor vai de -2 (muito
    // cauteloso) a +2 (muito valoroso).
    public static class KingTemperament
    {
        public static int Boldness(Hero hero)
        {
            if (hero == null)
            {
                return 0;
            }

            return hero.GetTraitLevel(DefaultTraits.Valor);
        }

        public static bool IsCautious(Hero hero)
        {
            return Boldness(hero) <= -1;
        }

        public static bool IsBold(Hero hero)
        {
            return Boldness(hero) >= 1;
        }

        // Rei valoroso declara guerra com mais frequencia; cauteloso, menos.
        public static float WarChanceMultiplier(Hero king)
        {
            float value = 1f + 0.2f * Boldness(king);

            if (value < 0.5f)
            {
                return 0.5f;
            }

            if (value > 1.5f)
            {
                return 1.5f;
            }

            return value;
        }
    }
}
