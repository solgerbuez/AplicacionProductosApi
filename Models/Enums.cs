namespace AppProductos.Models;

public enum EstadoVenta
{
    Pendiente,   // carrito confirmado, falta pagar
    Pagada,
    Cancelada
}

public enum EstadoPago
{
    Aprobado,
    Rechazado
}
