using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.Storage
{
    public class TopMostRecord
    {
        [JsonInclude]
        public ConcurrentDictionary<string, Record> records { get; private set; } = new ConcurrentDictionary<string, Record>();

        internal bool IsTopMost(Result result)
        {
            if (records.IsEmpty || result.OriginQuery == null ||
                !records.TryGetValue(result.OriginQuery.RawQuery, out var value))
            {
                return false;
            }

            // since this dictionary should be very small (or empty) going over it should be pretty fast.
            return value.Equals(result, result.TitleEqualRegex, result.SubTitleEqualRegex);
        }

        internal void Remove(Result result)
        {
            records.Remove(result.OriginQuery.RawQuery, out _);
        }

        internal void AddOrUpdate(Result result)
        {
            var record = new Record
            {
                PluginID = result.PluginID,
                Title = result.Title,
                SubTitle = result.SubTitle
            };
            records.AddOrUpdate(result.OriginQuery.RawQuery, record, (key, oldValue) => record);
        }

        public void Load(Dictionary<string, Record> dictionary)
        {
            records = new ConcurrentDictionary<string, Record>(dictionary);
        }
    }

    public class Record
    {
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string PluginID { get; set; }

        public bool Equals(Result r, Regex titleMatchRegex = null, Regex subTitleMatchRegex = null)
        {
            var titleEqual = titleMatchRegex == null ? Title == r.Title :
                new AdvancedStringComparer(titleMatchRegex).Equal(Title, r.Title);
            var subTitleEqual = subTitleMatchRegex == null ? SubTitle == r.SubTitle :
                new AdvancedStringComparer(subTitleMatchRegex).Equal(SubTitle, r.SubTitle);
            return titleEqual
                && subTitleEqual
                && PluginID == r.PluginID;
        }

        private class AdvancedStringComparer
        {
            private readonly Regex _regex;

            public AdvancedStringComparer(Regex compareRegex)
            {
                _regex = compareRegex;
            }

            // Method to compare two strings
            public bool Equal(string str1, string str2)
            {
                var match1 = _regex.Match(str1);
                var match2 = _regex.Match(str2);

                // Only if both strings match the regex, we compare the relevant parts
                var matchSuccess = match1.Success && match2.Success;
                if (matchSuccess)
                {
                    // Compare the matched parts of the strings
                    var matchGroup1 = match1.Groups;
                    var matchGroup2 = match2.Groups;

                    // If the number of matched groups is different, the strings are not equal
                    if (matchGroup1.Count != matchGroup2.Count)
                    {
                        return false;
                    }

                    // If user uses the entire match
                    if (matchGroup1.Count == 1)
                    {
                        return matchGroup1[0].Value == matchGroup2[0].Value;
                    }

                    // If user uses the named groups,
                    // Skip the first group which is the entire match
                    for (var i = 1; i < matchGroup1.Count; i++)
                    {
                        // Compare the matched values
                        var matchValue1 = matchGroup1[i].Value;
                        var matchValue2 = matchGroup2[i].Value;
                        if (matchValue1 != matchValue2)
                        {
                            return false;
                        }
                    }

                    return true;
                }
                else
                {
                    // If one or both strings do not match the regex, we compare original strings as is
                    return str1 == str2;
                }
            }
        }
    }
}
