namespace MaestroNotes.Data
{
    public class RtfPerson
    {
        public string _Name { get; set; } = "";
        public string _Vorname { get; set; } = "";
        public string _Alias { get; set; } = "";
        public string Name => _Alias == "" ? _Vorname + " " + _Name : _Alias;
        public string OrderName => _Name != "" && _Vorname != "" ? _Name + ", " + _Vorname : _Alias;
        public void Update()
        {
            if (!_Alias.Contains(",") && !_Alias.Contains("u.a.") && !_Alias.Contains("u. a."))
            {
                // wirklicher Name
                string[] parts = _Alias.Split(' ');
                if (parts.Length == 1)
                {
                    _Name = parts[0];
                    _Alias = _Vorname = "";
                }
                else if (parts.Length == 2)
                {
                    _Name = parts[1];
                    _Vorname = parts[0];
                    _Alias = "";
                }
                else
                {
                    // van xyz
                    if (parts[0].Equals("Sir"))
                    {
                        _Name = parts[0] + " " + parts[2];
                        _Vorname = parts[1];
                        return;
                    }
                    if (parts[1].Equals("van"))
                    {
                        _Name = parts[1] + " " + parts[2];
                        _Vorname = parts[0];
                        return;
                    }
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        if (_Vorname != "")
                            _Vorname += " ";
                        _Vorname += parts[i];
                    }
                    _Name = parts[parts.Length - 1];
                }
            }
        }
    }
}