namespace Gramatik.App.Models;

public interface ISecretProtector
{
    string Protect(string value);

    string Unprotect(string protectedValue);
}
