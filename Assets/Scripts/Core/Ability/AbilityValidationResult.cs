namespace ProjectTowerRpg.Core.Abilities
{
    public struct CastValidationResult
    {
        public bool IsPossible;
        public string Reason; // null, "NO_TARGET", "OUT_OF_RANGE", "INVALID_TARGET"
    }
}

