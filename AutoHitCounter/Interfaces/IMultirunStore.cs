//

using AutoHitCounter.Models;

namespace AutoHitCounter.Interfaces;

public interface IMultirunStore
{
    MultirunConfig Load();
    void Save(MultirunConfig config);
}
