using System.Runtime.InteropServices;
using Bit.Core.Dirt.Reports.Models.Data;

namespace Bit.Core.Dirt.Reports.ReportFeatures;

/// <summary>
/// Counts, per member, the distinct organization-owned ciphers reachable through the collections that
/// member can access. Both repository implementations share this so the two backends cannot diverge.
/// </summary>
public static class SharedItemCountCalculator
{
    /// <summary>
    /// Returns the distinct reachable cipher count keyed by organization user id. Members with no
    /// reachable ciphers are omitted; callers should treat a missing key as zero.
    /// </summary>
    public static Dictionary<Guid, int> Calculate(
        IReadOnlyCollection<MemberCollectionAccess> access,
        IReadOnlyCollection<CollectionCipherLink> content)
    {
        if (access.Count == 0 || content.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var links = AsSpan(content);

        var collectionIndexes = new Dictionary<Guid, int>();
        var cipherIndexes = new Dictionary<Guid, int>();
        var linkCollections = new int[links.Length];
        var linkCiphers = new int[links.Length];

        for (var i = 0; i < links.Length; i++)
        {
            var link = links[i];

            if (!collectionIndexes.TryGetValue(link.CollectionId, out var collection))
            {
                collection = collectionIndexes.Count;
                collectionIndexes[link.CollectionId] = collection;
            }

            if (!cipherIndexes.TryGetValue(link.CipherId, out var cipher))
            {
                cipher = cipherIndexes.Count;
                cipherIndexes[link.CipherId] = cipher;
            }

            linkCollections[i] = collection;
            linkCiphers[i] = cipher;
        }

        var collectionCount = collectionIndexes.Count;

        // A cipher reachable through only one collection can never be reached twice by the same member, so
        // those ciphers are counted in bulk per collection instead of being deduplicated one at a time.
        // cipherSlots holds the link count per cipher first, then the cipher's slot in the stamp array,
        // or -1 when the cipher needs no stamping.
        var cipherSlots = new int[cipherIndexes.Count];
        for (var i = 0; i < linkCiphers.Length; i++)
        {
            cipherSlots[linkCiphers[i]]++;
        }

        var sharedCipherCount = 0;
        for (var i = 0; i < cipherSlots.Length; i++)
        {
            cipherSlots[i] = cipherSlots[i] == 1 ? -1 : sharedCipherCount++;
        }

        var exclusiveCounts = new int[collectionCount];
        var sharedStarts = new int[collectionCount + 1];
        for (var i = 0; i < links.Length; i++)
        {
            if (cipherSlots[linkCiphers[i]] < 0)
            {
                exclusiveCounts[linkCollections[i]]++;
            }
            else
            {
                sharedStarts[linkCollections[i] + 1]++;
            }
        }

        for (var i = 0; i < collectionCount; i++)
        {
            sharedStarts[i + 1] += sharedStarts[i];
        }

        var sharedCiphers = new int[sharedStarts[collectionCount]];
        var sharedCursors = new int[collectionCount];
        Array.Copy(sharedStarts, sharedCursors, collectionCount);
        for (var i = 0; i < links.Length; i++)
        {
            var slot = cipherSlots[linkCiphers[i]];
            if (slot >= 0)
            {
                sharedCiphers[sharedCursors[linkCollections[i]]++] = slot;
            }
        }

        var edges = AsSpan(access);

        var memberIndexes = new Dictionary<Guid, int>();
        var memberIds = new List<Guid>();
        var memberDegrees = new List<int>();
        for (var i = 0; i < edges.Length; i++)
        {
            var edge = edges[i];
            if (!collectionIndexes.ContainsKey(edge.CollectionId))
            {
                continue;
            }

            if (!memberIndexes.TryGetValue(edge.OrganizationUserId, out var member))
            {
                member = memberIds.Count;
                memberIndexes[edge.OrganizationUserId] = member;
                memberIds.Add(edge.OrganizationUserId);
                memberDegrees.Add(0);
            }

            memberDegrees[member]++;
        }

        var memberCount = memberIds.Count;
        if (memberCount == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var memberStarts = new int[memberCount + 1];
        for (var i = 0; i < memberCount; i++)
        {
            memberStarts[i + 1] = memberStarts[i] + memberDegrees[i];
        }

        var memberCollections = new int[memberStarts[memberCount]];
        var memberCursors = new int[memberCount];
        Array.Copy(memberStarts, memberCursors, memberCount);
        for (var i = 0; i < edges.Length; i++)
        {
            var edge = edges[i];
            if (collectionIndexes.TryGetValue(edge.CollectionId, out var collection))
            {
                memberCollections[memberCursors[memberIndexes[edge.OrganizationUserId]]++] = collection;
            }
        }

        var stamps = new int[sharedCipherCount];
        var stampToken = 0;
        var countsByAccessSet = new Dictionary<AccessSet, int>(new AccessSetComparer(memberCollections));
        var result = new Dictionary<Guid, int>(memberCount);

        for (var member = 0; member < memberCount; member++)
        {
            var start = memberStarts[member];
            var length = memberStarts[member + 1] - start;

            // Sorting and deduplicating in place turns the access set into a canonical signature, so members
            // granted the same collections through different groups share one computation.
            if (length > 1)
            {
                Array.Sort(memberCollections, start, length);

                var write = start + 1;
                for (var read = start + 1; read < start + length; read++)
                {
                    if (memberCollections[read] != memberCollections[write - 1])
                    {
                        memberCollections[write++] = memberCollections[read];
                    }
                }

                length = write - start;
            }

            var accessSet = new AccessSet(start, length);
            if (!countsByAccessSet.TryGetValue(accessSet, out var count))
            {
                stampToken++;

                for (var i = 0; i < length; i++)
                {
                    var collection = memberCollections[start + i];
                    count += exclusiveCounts[collection];

                    var sharedEnd = sharedStarts[collection + 1];
                    for (var shared = sharedStarts[collection]; shared < sharedEnd; shared++)
                    {
                        ref var stamp = ref stamps[sharedCiphers[shared]];
                        if (stamp != stampToken)
                        {
                            stamp = stampToken;
                            count++;
                        }
                    }
                }

                countsByAccessSet[accessSet] = count;
            }

            if (count > 0)
            {
                result[memberIds[member]] = count;
            }
        }

        return result;
    }

    /// <summary>
    /// Enumerating through the interface costs a dispatch per element, and these inputs run to millions of
    /// edges, so the two shapes the repositories actually pass are read directly.
    /// </summary>
    private static ReadOnlySpan<T> AsSpan<T>(IReadOnlyCollection<T> source)
    {
        switch (source)
        {
            case T[] array:
                return array;
            case List<T> list:
                return CollectionsMarshal.AsSpan(list);
            default:
                var copy = new T[source.Count];
                var index = 0;
                foreach (var item in source)
                {
                    copy[index++] = item;
                }

                return copy;
        }
    }

    /// <summary>
    /// A member's normalized access set, as a range within the shared collection-index buffer.
    /// </summary>
    private readonly record struct AccessSet(int Start, int Length);

    private sealed class AccessSetComparer(int[] collections) : IEqualityComparer<AccessSet>
    {
        public bool Equals(AccessSet x, AccessSet y)
        {
            if (x.Length != y.Length)
            {
                return false;
            }

            if (x.Start == y.Start)
            {
                return true;
            }

            return collections.AsSpan(x.Start, x.Length).SequenceEqual(collections.AsSpan(y.Start, y.Length));
        }

        public int GetHashCode(AccessSet accessSet)
        {
            var hash = new HashCode();
            hash.AddBytes(MemoryMarshal.AsBytes(collections.AsSpan(accessSet.Start, accessSet.Length)));
            return hash.ToHashCode();
        }
    }
}
