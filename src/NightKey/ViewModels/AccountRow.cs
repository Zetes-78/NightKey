using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using NightKey.Core;

namespace NightKey.ViewModels;

public sealed class AccountRow : INotifyPropertyChanged
{
    private readonly byte[] _key;
    private long _counter = long.MinValue;
    private string _code = "";
    private Geometry _moon = Geometry.Empty;
    private bool _isExpiring;
    private int _secondsLeft;

    public AccountRow(Account account)
    {
        Account = account;
        _key = Base32.Decode(account.Secret);
    }

    public Account Account { get; }
    public string Title => Account.DisplayTitle;
    public string Subtitle => Account.DisplaySubtitle;

    public string Code { get => _code; private set { if (Set(ref _code, value)) OnChanged(nameof(DisplayCode)); } }

    public string DisplayCode => _code.Length switch
    {
        6 or 7 => _code[..3] + " " + _code[3..],
        8 => _code[..4] + " " + _code[4..],
        _ => _code,
    };

    public Geometry Moon { get => _moon; private set => Set(ref _moon, value); }
    public bool IsExpiring { get => _isExpiring; private set => Set(ref _isExpiring, value); }
    public int SecondsLeft { get => _secondsLeft; private set => Set(ref _secondsLeft, value); }

    public void Tick(DateTimeOffset now)
    {
        long ms = now.ToUnixTimeMilliseconds();
        long periodMs = Account.Period * 1000L;
        long counter = ms / periodMs;
        if (counter != _counter)
        {
            _counter = counter;
            Code = Totp.Hotp(_key, counter, Account.Digits, Account.Algorithm);
        }

        double fraction = 1.0 - (ms % periodMs) / (double)periodMs;
        Moon = MoonShape.Build(fraction, 13, 14, 14);
        SecondsLeft = (int)Math.Ceiling(fraction * Account.Period);
        IsExpiring = SecondsLeft <= 5;
    }

    public void NotifyLabels()
    {
        OnChanged(nameof(Title));
        OnChanged(nameof(Subtitle));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool Set<T>(ref T field, T value, [CallerMemberName] string name = "")
    {
        if (Equals(field, value)) return false;
        field = value;
        OnChanged(name);
        return true;
    }
}
