﻿// Copyright (c) IxMilia.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace IxMilia.Classics
{
    public class LatinDictionary
    {
        private readonly Dictionary<string, HashSet<string>> _suffixes = new Dictionary<string, HashSet<string>>();
        private readonly Dictionary<string, DictionaryEntry> _entries = new Dictionary<string, DictionaryEntry>();
        private readonly Regex _definitionMatcher = new Regex(@"^#(.*)  ([A-Z].*)\[(.....)\] :: (.*)$");
        //                                                        ^^^^ word
        //                                                              ^^^^^^^^^ part of speech
        //                                                                       ^^^^^^^^^^^ flags
        //                                                                                      ^^^^ definition

        private Assembly CurrentAssembly => typeof(LatinDictionary).GetTypeInfo().Assembly;

        private IEnumerable<string> SuffixWords => new[] { "ne", "que" };

        public LatinDictionary()
        {
            LoadSuffixes();
            LoadDictionary();
        }

        public DictionaryEntry[] GetDefinitions(string word)
        {
            word = word.ToLowerInvariant();
            foreach (var possibleRoot in PossibleRootWords(word))
            {
                if (_entries.ContainsKey(possibleRoot))
                {
                    // TODO: Find all possible definitions.  Error if more than one are found.  Need a way to force disambiguation.
                    return new[] { _entries[possibleRoot] };
                }
            }

            foreach (var suffixWord in SuffixWords.Select(s => s.ToLowerInvariant()))
            {
                var suffixDefinitions = GetDefinitions(suffixWord);
                if (suffixDefinitions.Length > 0 && word.EndsWith(suffixWord))
                {
                    word = word.Substring(0, word.Length - suffixWord.Length);
                    var def = GetDefinitions(word);
                    if (def.Length > 0)
                    {
                        return def.Concat(suffixDefinitions).ToArray();
                    }
                }
            }

            return new DictionaryEntry[0];
        }

        private IEnumerable<string> PossibleRootWords(string word)
        {
            yield return word;
            foreach (var suffixKey in _suffixes)
            {
                if (word.EndsWith(suffixKey.Key))
                {
                    var stem = word.Substring(0, word.Length - suffixKey.Key.Length);
                    foreach (var potentialSufix in suffixKey.Value)
                    {
                        yield return stem + potentialSufix;
                    }
                }
            }
        }

        private void LoadSuffixes()
        {
            var suffixStream = CurrentAssembly.GetManifestResourceStream("IxMilia.Classics.LatinSuffixes.txt");
            var streamReader = new StreamReader(suffixStream);
            for (var line = streamReader.ReadLine(); line != null; line = streamReader.ReadLine())
            {
                if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                {
                    // skip blank and comment lines
                    continue;
                }

                var parts = line.Split("|".ToCharArray(), 2);
                var suffix = parts[0];
                var replacements = new HashSet<string>(parts[1].Split(",".ToCharArray()));
                if (_suffixes.ContainsKey(suffix))
                {
                    // append more possibilities
                    foreach (var existing in _suffixes[suffix])
                    {
                        replacements.Add(existing);
                    }
                }

                _suffixes[suffix] = replacements;
            }
        }

        private void LoadDictionary()
        {
            var dictionaryStream = CurrentAssembly.GetManifestResourceStream("IxMilia.Classics.Downloads.DICTPAGE.RAW");
            var streamReader = new StreamReader(dictionaryStream);
            for (var line = streamReader.ReadLine(); line != null; line = streamReader.ReadLine())
            {
                // each line in the dictionary looks like this:
                // #word  part of speech       ...           [XXXXX] :: definition;
                //      ^^ that's two spaces         char 102 ^
                // the five characters starting at position 102 give additional information about the word:
                // column 1 - age
                //   X = used throughout ages/unknown (default)
                //   A = archaic
                //   B = early
                //   C = classical
                //   D = late (3rd-5th C)
                //   E = later (6th-10th C)
                //   F = medieval (11th-15th C)
                //   G = scholar (15th+ C)
                //   H = modern (19th-21st C)
                // column 2 - area
                //   X = all or none
                //   A = agriculture, flora, fauna, land, equipment, rural
                //   B = biological, medical, body parts
                //   D = drama, music, theater, art, painting, sculpture
                //   E = ecclesiastic, biblical, religious
                //   G = grammar, rhetoric, logic, literature, schools
                //   L = legal, government, tax, financial, political, titles
                //   P = poetic
                //   S = science, philosophy, mathematics, units/measures
                //   T = technical, architecture, topography, surveying
                //   W = war, military, naval, ships, armor
                //   Y = mythology
                // column 3 - geography
                //   X = all or none
                //   A = Africa
                //   B = Britian
                //   C = China
                //   D = Scandinavia
                //   E = Egypt
                //   F = France, Gaul
                //   G = Germany
                //   H = Greece
                //   I = Italy, Rome
                //   J = India
                //   K = Balkans
                //   N = Netherlands
                //   P = Persia
                //   Q = Near East
                //   R = Russia
                //   S = Spain, Iberia
                //   U = Eastern Europe
                // column 4 - frequency
                //   X = unknown or unspecified
                //   A = very frequent - top 1000 words
                //   B = frequent - next 2000 words
                //   C = common - top 10000 words
                //   D = lesser - top 20000 words
                //   E = uncommon = 2-3 citations
                //   F = very rare = 1 citation
                //   I = inscription
                //   M = graffiti
                //   N = Pliny
                // column 5 - source
                //   X = General or unknown or too common to say
                //   A =
                //   B = C.H.Beeson, A Primer of Medieval Latin, 1925(Bee)
                //   C = Charles Beard, Cassell's Latin Dictionary 1892 (CAS)
                //   D = J.N.Adams, Latin Sexual Vocabulary, 1982(Sex)
                //   E = L.F.Stelten, Dictionary of Eccles.Latin, 1995(Ecc)
                //   F = Roy J.Deferrari, Dictionary of St.Thomas Aquinas, 1960(DeF)
                //   G = Gildersleeve + Lodge, Latin Grammar 1895(G + L)
                //   H = Collatinus Dictionary by Yves Ouvrard
                //   I = Leverett, F.P., Lexicon of the Latin Language, Boston 1845
                //   J =
                //   K = Calepinus Novus, modern Latin, by Guy Licoppe (Cal)
                //   L = Lewis, C.S., Elementary Latin Dictionary 1891
                //   M = Latham, Revised Medieval Word List, 1980
                //   N = Lynn Nelson, Wordlist
                //   O = Oxford Latin Dictionary, 1982(OLD)
                //   P = Souter, A Glossary of Later Latin to 600 A.D., Oxford 1949
                //   Q = Other, cited or unspecified dictionaries
                //   R = Plater & White, A Grammar of the Vulgate, Oxford 1926
                //   S = Lewis and Short, A Latin Dictionary, 1879(L + S)
                //   T = Found in a translation--  no dictionary reference
                //   U = Du Cange
                //   V = Vademecum in opus Saxonis - Franz Blatt(Saxo)
                //   W = My personal guess
                //   Y = Temp special code
                //   Z = Sent by user, no dictionary reference
                var match = _definitionMatcher.Match(line);
                var entry = match.Groups[1].Value.Trim();
                var pos = match.Groups[2].Value.Trim();
                var flags = match.Groups[3].Value.Trim();
                var definition = match.Groups[4].Value.Trim();

                // TODO: handle duplicate entries
                _entries[GetEntryKey(entry)] = new DictionaryEntry(entry, definition, pos, flags);
            }
        }

        private static string GetEntryKey(string entry)
        {
            for (int i = 0; i < entry.Length; i++)
            {
                if (!char.IsLetter(entry[i]))
                {
                    return entry.Substring(0, i).ToLowerInvariant();
                }
            }

            return entry.ToLowerInvariant();
        }
    }
}