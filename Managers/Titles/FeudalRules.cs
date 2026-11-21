using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using CasusBelli.Managers.Titles.Governments;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;

namespace CasusBelli.Managers.Titles
{
    internal static class FeudalRules
    {
        internal static string LawId(string value) => (value ?? "").Trim().ToLowerInvariant() switch
        {
            "tribal" => "clan", "wilundingelective" or "battanianelective" or "tribalelective" or "aseraielective" or "dictatorship" => "feudal_elective",
            "feudalelective" => "feudal_elective", "agnaticcognatic" => "agnatic_cognatic", "absolutecognatic" => "absolute_cognatic",
            var id => id
        };
        internal static string SuccessionId(string value) => string.Equals(value,"Imperial",StringComparison.OrdinalIgnoreCase) ? "hereditary" : LawId(value);

        internal static void Validate(XmlNode root)
        {
            var realms = new HashSet<string>();
            var lands = new HashSet<string>();
            var errors = new List<string>();
            foreach (var kingdom in root.ChildNodes.OfType<XmlElement>())
            {
                string id = kingdom.GetAttribute("faction");
                if (kingdom.Name != "kingdom" || id.Length == 0 || !realms.Add(id))
                    errors.Add("Reino inválido ou repetido: " + id);
                bool present = Kingdom.All.Any(k => k.StringId == id);
                var duchies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (XmlElement node in kingdom.SelectNodes("descendant-or-self::*"))
                {
                    string key = node.GetAttribute("settlement");
                    switch (node.Name)
                    {
                        case "kingdom": break;
                        case "duchy":
                            if (node.ParentNode != kingdom || string.IsNullOrWhiteSpace(node.GetAttribute("name")) ||
                                !duchies.Add(node.GetAttribute("name").Replace(" ", "_"))) errors.Add("Ducado inválido em " + id);
                            break;
                        case "county":
                        case "barony":
                            if (node.ParentNode.Name != (node.Name == "county" ? "duchy" : "county") ||
                                key.Length == 0 || !lands.Add(key) || (present && Settlement.Find(key)?.IsFortification != true))
                                errors.Add("Feudo ou vínculo inválido: " + key);
                            break;
                        default: errors.Add("Elemento desconhecido: " + node.Name); break;
                    }
                    if (node.HasAttribute("government") && !DefaultGovernments.Instance.All.ContainsKey(LawId(node.GetAttribute("government"))) ||
                        node.HasAttribute("succession") && !DefaultSuccessions.Instance.All.ContainsKey(SuccessionId(node.GetAttribute("succession"))) ||
                        node.HasAttribute("inheritance") && !DefaultInheritances.Instance.All.ContainsKey(LawId(node.GetAttribute("inheritance"))) ||
                        node.HasAttribute("genderLaw") && !DefaultGenderLaws.Instance.All.ContainsKey(LawId(node.GetAttribute("genderLaw"))))
                        errors.Add("Lei desconhecida em " + node.OuterXml.Split('>')[0]);
                }
            }
            if (errors.Count > 0) throw new InvalidDataException(string.Join("\n", errors));
        }

        internal static FeudalContract Contract(XmlElement node, FeudalContract parent)
        {
            if (!node.HasAttribute("government") && !node.HasAttribute("succession") &&
                !node.HasAttribute("genderLaw") && !node.HasAttribute("inheritance")) return parent;
            string Law(string attribute,string fallback) => node.HasAttribute(attribute) ? node.GetAttribute(attribute) : fallback;
            return TitleGenerator.GenerateContract(LawId(Law("government",parent.Government.StringId)),
                SuccessionId(Law("succession",parent.Succession.StringId)),LawId(Law("inheritance",parent.Inheritance.StringId)),
                LawId(Law("genderLaw",parent.GenderLaw.StringId)));
        }

        internal static Hero PersonalHeir(Hero deceased, FeudalTitle title, int partition)
        {
            if (deceased.Clan == null) return null;
            var contract = title.Contract;
            var succession = contract?.Succession;
            var candidates = succession?.IsElective == true
                ? succession.GetSuccessionCandidates(deceased,title).AsEnumerable() : deceased.Clan.Heroes.AsEnumerable();
            var law = contract?.GenderLaw?.StringId;
            var eligible = candidates.Where(h => h != deceased && h.IsAlive && !h.IsDisabled && h.IsLord &&
                (law != "agnatic" || !h.IsFemale) && (succession?.IsElective != true || !h.IsChild)).ToList();
            if (eligible.Count == 0) return null;
            if (law == "agnatic_cognatic" && eligible.Any(h => !h.IsFemale)) eligible.RemoveAll(h => h.IsFemale);
            if (law == "absolute_cognatic" && eligible.Any(h => h.IsFemale)) eligible.RemoveAll(h => !h.IsFemale);
            if (succession?.IsElective == true)
                return eligible.OrderByDescending(h => TitleManager.Instance.GetModel().GetSuccessionHeirScore(deceased,h,title).ResultNumber)
                    .ThenBy(h => h.StringId,StringComparer.Ordinal).First();
            var inheritance = contract?.Inheritance?.StringId;
            if (inheritance != "seniority")
            {
                var children = eligible.Where(h => h.Father == deceased || h.Mother == deceased).ToList();
                if (children.Count > 0) eligible = children;
            }
            var ordered = eligible.OrderBy(h => inheritance == "ultimogeniture" ? h.Age : -h.Age)
                .ThenBy(h => h.StringId,StringComparer.Ordinal).ToList();
            return ordered[inheritance == "gavelkind" ? partition % ordered.Count : 0];
        }
    }
}
