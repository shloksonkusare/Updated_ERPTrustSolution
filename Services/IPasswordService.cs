namespace ERPTrustSolution.Services;

public interface IPasswordService
{
    string Encrypt(string plainText);
}