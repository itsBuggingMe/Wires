using Frent;
using Frent.Systems;
using System.Collections.Generic;

namespace Wires.Core;

internal static class Extensions
{
    public static IEnumerable<Entity> ToEnumerable(this Query query)
    {
        List<Entity> entities = [];

        foreach (Entity entity in query.EnumerateWithEntities())
            entities.Add(entity);

        return entities;
    }
}
