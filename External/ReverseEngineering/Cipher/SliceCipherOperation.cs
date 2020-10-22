namespace n0tFlix.YoutubeManager.ReverseEngineering.Cipher
{
    internal class SliceCipherOperation : ICipherOperation
    {
        private readonly int _index;

        public SliceCipherOperation(int index) => _index = index;

        public string Decipher(string input) => input.Substring(_index);

        public override string ToString() => $"Slice ({_index})";
    }
}