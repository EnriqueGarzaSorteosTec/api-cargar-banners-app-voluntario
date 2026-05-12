# Estructura del Proyecto

## Organización de Carpetas

```
ApiImagenesAppVoluntario/
??? Controllers/          # Controladores de la API
?   ??? ApiKeyHeadFilter.cs
?   ??? ImagenesController.cs
?   ??? WeatherForecastController.cs
??? Models/              # Modelos y DTOs
?   ??? CargarImagenRequest.cs
??? Services/            # Lógica de negocio
?   ??? ImagenService.cs
??? Helpers/             # Utilidades y funciones auxiliares
?   ??? ResponseHelper.cs
??? Attributes/          # Atributos personalizados
?   ??? ApiKeyAttribute.cs
??? Common.cs           # Funciones comunes de encriptación
```

## Descripción de Componentes

### Controllers/ImagenesController.cs
Controlador principal para manejo de imágenes. Contiene solo:
- Constructor con inyección de dependencias
- Endpoint `CargarImagen` - Carga y registra imágenes
- Endpoint `SubirBanner` - Método de prueba

### Models/CargarImagenRequest.cs
DTO para la solicitud de carga de imágenes con sus propiedades:
- `Archivo`: IFormFile con la imagen
- `IdTipoImagen`: Identificador del tipo
- `AltText`: Texto alternativo
- `Version`: Versión de la imagen

### Services/ImagenService.cs
Servicio para procesamiento de imágenes:
- `ObtenerDimensionesImagen()`: Obtiene dimensiones de PNG, JPEG y GIF
- Métodos privados para lectura de headers de cada formato

### Helpers/ResponseHelper.cs
Utilidades para procesamiento de respuestas:
- `ObtenerCodigoEstatusRespuesta()`: Extrae código de status de respuestas JSON

## Principios de Diseño

1. **Separación de Responsabilidades**: Cada clase tiene una responsabilidad única
2. **Inyección de Dependencias**: Los servicios se inyectan en los controladores
3. **Reutilización**: Métodos comunes en helpers y servicios
4. **Mantenibilidad**: Código organizado y fácil de localizar
