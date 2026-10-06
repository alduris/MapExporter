using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MapExporterNew.Generation
{
    internal class MusicEventProcessor(Generator owner) : Processor(owner)
    {
        public override string ProcessName => "Music events";

        protected override IEnumerator<float> Process()
        {
            List<MusicTriggerData> list = new List<MusicTriggerData>();
            foreach (var room in owner.regionInfo.rooms.Values)
            {
                float defaultOffset = -40f;
                foreach (var trigger in room.musicTriggers)
                {
                    if (trigger.song == "NO SONG") continue; // this happens in WORA because they get edited by a room script I think

                    Vector2 triggerPos;
                    if (trigger.pos != null)
                    {
                        triggerPos = trigger.pos.Value;
                    }
                    else
                    {
                        triggerPos = new Vector2(30f, room.size.y * 20f + defaultOffset);
                        defaultOffset -= 40f;
                    }
                    list.Add(new MusicTriggerData()
                    {
                        room = room.roomName,
                        songName = trigger.song,
                        triggerType = trigger.triggerType,
                        pos = room.devPos + triggerPos,
                    });
                }
            }
            owner.metadata["music_features"] = list;

            yield return 1f;
            yield break;
        }

        private struct MusicTriggerData : IJsonObject
        {
            public string room;
            public string songName;
            public string triggerType;
            public Vector2 pos;

            public readonly Dictionary<string, object> ToJson()
            {
                return new Dictionary<string, object>()
                {
                    { "type", "Feature" },
                    {
                        "geometry",
                        new Dictionary<string, object>
                        {
                            { "type", "Point" },
                            { "coordinates", Vector2ToArray(pos) }
                        }
                    },
                    {
                        "properties",
                        new Dictionary<string, object>
                        {
                            { "room", room },
                            { "song", songName },
                            { "triggerType", triggerType },
                        }
                    }
                };
            }
        }
    }
}
