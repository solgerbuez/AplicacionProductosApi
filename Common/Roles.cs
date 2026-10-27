namespace AppProductos.Common;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Operador = "Operador";
    public const string Cliente = "Cliente";

    // Para [Authorize(Roles = Roles.Staff)]
    public const string Staff = Admin + "," + Operador;

    public const int IdAdmin = 1;
    public const int IdOperador = 2;
    public const int IdCliente = 3;
}
