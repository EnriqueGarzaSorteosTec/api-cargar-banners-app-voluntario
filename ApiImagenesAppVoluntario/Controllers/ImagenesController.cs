using ApiImagenesAppVoluntario.Attributes;
using ApiImagenesAppVoluntario.Helpers;
using ApiImagenesAppVoluntario.Models;
using ApiImagenesAppVoluntario.Services;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using ProveedorDatos;
using SorteosTec.Helpers;
using Swashbuckle.AspNetCore.Annotations;

namespace ApiImagenesAppVoluntario.Controllers
{
    [ApiKey]
    [Route("[controller]")]
    [ApiController]
    public class ImagenesController : Controller
    {
        private readonly ILogger<ImagenesController> _logger;
        private readonly IConfiguration _configuracion;
        private readonly ImagenService _imagenService;

        public ImagenesController(
            ILogger<ImagenesController> logger, 
            IConfiguration configuracion,
            ImagenService imagenService)
        {
            _logger = logger;
            _configuracion = configuracion;
            _imagenService = imagenService;
            LogHelper.Ruta = _configuracion["rutaLogs"];
            LogHelper.NombreArchivo = _configuracion["nombreLog"];
        }

        /// <summary>Carga una imagen al servidor y retorna su información.</summary>
        /// <response code="200">Imagen cargada exitosamente</response>
        /// <response code="400">Error en la carga</response>
        [HttpPost("/GuardarImagen")]
        [SwaggerOperation("GuardarImagen")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> GuardarImagen([FromForm] CargarImagenRequest request)
        {
            string apiKey = ControllerContext.HttpContext.Request.Headers["ApiKey"];
            string direccionIP = ControllerContext.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            string rutaCompleta = string.Empty;
            Guid guidImagen = Guid.NewGuid(); // ← DECLARAR AQUÍ UNA SOLA VEZ

            try
            {
                // Validar que se haya enviado un archivo
                if (request.Archivo == null || request.Archivo.Length == 0)
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { error = "No se ha seleccionado ningún archivo" });
                }

                // Validar que sea una imagen
                var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(request.Archivo.FileName).ToLowerInvariant();
                
                if (!extensionesPermitidas.Contains(extension))
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { error = "El archivo debe ser una imagen (jpg, jpeg, png, webp)" });
                }

                // Obtener el directorio de configuración
                string directorioImagenes = _configuracion["DirectorioImagenes"];
                string direccionPublica = _configuracion["DireccionImagenesPublica"];

                if (string.IsNullOrEmpty(directorioImagenes))
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { error = "No se ha configurado el directorio de imágenes en appsettings.json" });
                }

                // Crear el directorio si no existe
                if (!Directory.Exists(directorioImagenes))
                {
                    Directory.CreateDirectory(directorioImagenes);
                }

                // CAMBIO: Usar el guidImagen ya declarado
                string nombreArchivo = $"{guidImagen}{extension}";
                rutaCompleta = Path.Combine(directorioImagenes, nombreArchivo);

                // Guardar el archivo
                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await request.Archivo.CopyToAsync(stream);
                }

                // Obtener dimensiones y peso de la imagen
                int ancho = 0;
                int alto = 0;
                long peso = new FileInfo(rutaCompleta).Length;

                // Obtener dimensiones
                var dimensiones = _imagenService.ObtenerDimensionesImagen(rutaCompleta);
                ancho = dimensiones.ancho;
                alto = dimensiones.alto;

                // Construir la URL pública
                string urlPublica = direccionPublica?.TrimEnd('/') + "/" + nombreArchivo;

                // CAMBIO: Usar guidImagen.ToString() en vez de Guid.NewGuid().ToString()
                var datosImagen = new
                {
                    guid_imagen = guidImagen.ToString(),
                    url = urlPublica,
                    id_tipo_imagen = request.IdTipoImagen,
                    alt_text = request.AltText ?? Path.GetFileNameWithoutExtension(request.Archivo.FileName),
                    alto = alto,
                    ancho = ancho,
                    peso = peso,
                    version = request.Version
                };

                // Serializar a JSON para enviar al procedimiento almacenado
                string jsonDatosImagen = System.Text.Json.JsonSerializer.Serialize(datosImagen);

                
                LogHelper.RegistrarLog($"JSON Entrada a BD: {jsonDatosImagen}");

                // Registrar en la base de datos
                Operacion.EsValidacionEnOrapro = true;
                string jsonResultadoBD = Operacion.Ejecutar(
                    apiKey, 
                    direccionIP, 
                    "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Guardar_Datos_Imagen", 
                    "p_json",
                    jsonDatosImagen,
                    "p_out_cursor"
                );

                LogHelper.RegistrarLog($"Imagen cargada exitosamente: {nombreArchivo} - URL: {urlPublica}");
                LogHelper.RegistrarLog($"Respuesta BD: {jsonResultadoBD}");

                // Verificar que el registro en BD fue exitoso
                JObject resultadoBD = JObject.Parse(jsonResultadoBD);
                int exitoBD = resultadoBD["exito"]?.Value<int>() ?? 0;
                string mensajeBD = resultadoBD["mensaje"]?.Value<string>() ?? "Sin mensaje";

                if (exitoBD != 1)
                {
                    // Si hubo error en BD, eliminar el archivo físico
                    if (System.IO.File.Exists(rutaCompleta))
                    {
                        System.IO.File.Delete(rutaCompleta);
                        LogHelper.RegistrarLog($"Archivo eliminado debido a error en BD: {nombreArchivo}");
                    }

                    return StatusCode(StatusCodes.Status400BadRequest, new 
                    { 
                        error = "Error al registrar la imagen en la base de datos",
                        mensaje = mensajeBD,
                        detalle_bd = jsonResultadoBD
                    });
                }

                // CAMBIO: Usar guidImagen.ToString() en vez de Guid.NewGuid().ToString()
                var respuesta = new
                {
                    guid_imagen = guidImagen.ToString(),
                    url = urlPublica,
                    id_tipo_imagen = request.IdTipoImagen,
                    alt_text = request.AltText ?? Path.GetFileNameWithoutExtension(request.Archivo.FileName),
                    alto = alto,
                    ancho = ancho,
                    peso = peso,
                    version = request.Version,
                    nombre_archivo = nombreArchivo,
                    fecha_carga = DateTime.Now,
                    registrado_bd = true,
                    mensaje = mensajeBD
                };

                return StatusCode(StatusCodes.Status200OK, respuesta);
            }
            catch (Newtonsoft.Json.JsonException jsonEx)
            {
                // Error al parsear la respuesta de la BD - eliminar archivo
                if (!string.IsNullOrEmpty(rutaCompleta) && System.IO.File.Exists(rutaCompleta))
                {
                    System.IO.File.Delete(rutaCompleta);
                    LogHelper.RegistrarLog($"Archivo eliminado debido a error de JSON en respuesta BD: {rutaCompleta}");
                }

                var errorDetails = new
                {
                    Message = "Error al procesar la respuesta de la base de datos",
                    DetalleError = jsonEx.Message,
                    StackTrace = jsonEx.StackTrace,
                    Parameters = new
                    {
                        ApiKey = apiKey?.Substring(0, Math.Min(5, apiKey?.Length ?? 0)) + "...",
                        DireccionIP = direccionIP,
                        NombreArchivo = request?.Archivo?.FileName,
                        GuidImagen = guidImagen.ToString(),  // ← AGREGAR
                        Procedimiento = "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Guardar_Datos_Imagen"
                    }
                };

                LogHelper.RegistrarLog(System.Text.Json.JsonSerializer.Serialize(errorDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                return StatusCode(StatusCodes.Status500InternalServerError, new 
                { 
                    error = "Error al procesar respuesta de la base de datos", 
                    details = jsonEx.Message 
                });
            }
            catch (Exception excepcion)
            {
                // Error general - eliminar archivo si existe
                if (!string.IsNullOrEmpty(rutaCompleta) && System.IO.File.Exists(rutaCompleta))
                {
                    try
                    {
                        System.IO.File.Delete(rutaCompleta);
                        LogHelper.RegistrarLog($"Archivo eliminado debido a excepción: {rutaCompleta}");
                    }
                    catch (Exception deleteEx)
                    {
                        LogHelper.RegistrarLog($"Error al eliminar archivo: {deleteEx.Message}");
                    }
                }

                var errorDetails = new
                {
                    Message = excepcion.Message,
                    StackTrace = excepcion.StackTrace,
                    InnerException = excepcion.InnerException?.Message,
                    Source = excepcion.Source,
                    Parameters = new
                    {
                        ApiKey = apiKey?.Substring(0, Math.Min(5, apiKey?.Length ?? 0)) + "...",
                        DireccionIP = direccionIP,
                        NombreArchivo = request?.Archivo?.FileName,
                        IdTipoImagen = request?.IdTipoImagen,
                        GuidImagen = guidImagen.ToString(),  // ← AGREGAR
                        Procedimiento = "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Guardar_Datos_Imagen"
                    }
                };

                LogHelper.RegistrarLog(System.Text.Json.JsonSerializer.Serialize(errorDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                return StatusCode(StatusCodes.Status400BadRequest, new { error = excepcion.Message, details = excepcion.ToString() });
            }
        }


        /// <summary>Obtiene un nuevo identificador de agrupador.</summary>
        /// <response code="200">Proceso Exitoso</response>
        /// <response code="400">Error al banners.</response>        
        [HttpGet("/ObtenerImagenesTipoBanners")]
        [SwaggerOperation("ObtenerImagenesTipoBanners")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        public IActionResult ObtenerImagenesTipoBanners()
        {
            IActionResult actionResult = StatusCode(StatusCodes.Status400BadRequest);
            string jsonResultado = string.Empty;
            string apiKey = ControllerContext.HttpContext.Request.Headers["ApiKey"];
            string direccionIP = ControllerContext.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            try
            {
                apiKey = apiKey != null ? apiKey : "";

                Operacion.EsValidacionEnOrapro = true;

                jsonResultado = Operacion.Ejecutar(apiKey, direccionIP, "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Obtener_Imagenes_Tipo_Banner", "p_out_cursor");
                
                // Debug: Log the actual response to see what's being returned
                LogHelper.RegistrarLog($"Raw response from Oracle: {jsonResultado}");
                
                int codigoEstatus = ResponseHelper.ObtenerCodigoEstatusRespuesta(jsonResultado, StatusCodes.Status200OK);

                return StatusCode(codigoEstatus, jsonResultado);
            }
            catch (Exception excepcion)
            {
                var errorDetails = new
                {
                    Message = excepcion.Message,
                    StackTrace = excepcion.StackTrace,
                    InnerException = excepcion.InnerException?.Message,
                    Source = excepcion.Source,
                    RawResponse = jsonResultado,
                    Parameters = new
                    {
                        NombreConexion = _configuracion["nombreConexion"],
                        ApiKey = apiKey?.Substring(0, Math.Min(5, apiKey?.Length ?? 0)) + "...",
                        DireccionIP = direccionIP,
                        Procedure = "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Obtener_Imagenes_Tipo_Banner"
                    }
                };
                
                LogHelper.RegistrarLog(System.Text.Json.JsonSerializer.Serialize(errorDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                
                return StatusCode(StatusCodes.Status400BadRequest, new { error = excepcion.Message, details = excepcion.ToString() });
            }
        }


        /// <summary>Obtiene la información de una imagen.</summary>
        /// <response code="200">Imagen encontrada exitosamente</response>
        /// <response code="400">Error al obtener la imagen</response>
        [HttpPost("/ObtenerImagen")]
        [SwaggerOperation("ObtenerImagen")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
        public IActionResult ObtenerImagen([FromBody] ObtenerImagenRequest request)
        {
            string apiKey = ControllerContext.HttpContext.Request.Headers["ApiKey"];
            string direccionIP = ControllerContext.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            try
            {
                // Validar que se haya enviado el guid
                if (string.IsNullOrEmpty(request.guid_imagen))
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { error = "El campo guid_imagen es requerido" });
                }

                // Validar formato de GUID
                if (!Guid.TryParse(request.guid_imagen, out _))
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { error = "El guid_imagen no tiene un formato válido" });
                }

                // Preparar JSON para enviar a BD
                var jsonEntrada = new
                {
                    guid_imagen = request.guid_imagen
                };

                string jsonDatosImagen = System.Text.Json.JsonSerializer.Serialize(jsonEntrada);
                
                LogHelper.RegistrarLog($"JSON Entrada a BD (ObtenerImagen): {jsonDatosImagen}");

                // Llamar al procedimiento almacenado
                Operacion.EsValidacionEnOrapro = true;
                string jsonResultadoBD = Operacion.Ejecutar(
                    apiKey,
                    direccionIP,
                    "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Obtener_Datos_Imagen",
                    "p_json",
                    jsonDatosImagen,
                    "p_out_cursor"
                );

                LogHelper.RegistrarLog($"Respuesta BD (ObtenerImagen): {jsonResultadoBD}");

                // Parsear respuesta de BD
                JObject resultadoBD = JObject.Parse(jsonResultadoBD);
                int exitoBD = resultadoBD["exito"]?.Value<int>() ?? 0;
                string mensajeBD = resultadoBD["mensaje"]?.Value<string>() ?? "Sin mensaje";

                if (exitoBD != 1)
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new
                    {
                        error = "Error al obtener la imagen de la base de datos",
                        mensaje = mensajeBD,
                        detalle_bd = jsonResultadoBD
                    });
                }

                // Devolver la respuesta completa de la BD (incluye imagen)
                return StatusCode(StatusCodes.Status200OK, jsonResultadoBD);
            }
            catch (Newtonsoft.Json.JsonException jsonEx)
            {
                var errorDetails = new
                {
                    Message = "Error al procesar la respuesta de la base de datos",
                    DetalleError = jsonEx.Message,
                    StackTrace = jsonEx.StackTrace,
                    Parameters = new
                    {
                        ApiKey = apiKey?.Substring(0, Math.Min(5, apiKey?.Length ?? 0)) + "...",
                        DireccionIP = direccionIP,
                        GuidImagen = request.guid_imagen,
                        Procedimiento = "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Obtener_Datos_Imagen"
                    }
                };

                LogHelper.RegistrarLog(System.Text.Json.JsonSerializer.Serialize(errorDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Error al procesar respuesta de la base de datos",
                    details = jsonEx.Message
                });
            }
            catch (Exception excepcion)
            {
                var errorDetails = new
                {
                    Message = excepcion.Message,
                    StackTrace = excepcion.StackTrace,
                    InnerException = excepcion.InnerException?.Message,
                    Source = excepcion.Source,
                    Parameters = new
                    {
                        ApiKey = apiKey?.Substring(0, Math.Min(5, apiKey?.Length ?? 0)) + "...",
                        DireccionIP = direccionIP,
                        GuidImagen = request.guid_imagen,
                        Procedimiento = "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Obtener_Datos_Imagen"
                    }
                };

                LogHelper.RegistrarLog(System.Text.Json.JsonSerializer.Serialize(errorDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                return StatusCode(StatusCodes.Status400BadRequest, new { error = excepcion.Message, details = excepcion.ToString() });
            }
        }

        /// <summary>Elimina una imagen del servidor y de la base de datos.</summary>
        /// <response code="200">Imagen eliminada exitosamente</response>
        /// <response code="400">Error en la eliminación</response>
        [HttpPost("/EliminarImagen")]
        [SwaggerOperation("EliminarImagen")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
        public IActionResult EliminarImagen([FromBody] EliminarImagenRequest request)
        {
            string apiKey = ControllerContext.HttpContext.Request.Headers["ApiKey"];
            string direccionIP = ControllerContext.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            try
            {
                // Validar que se haya enviado el guid
                if (string.IsNullOrEmpty(request.guid_imagen))
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { error = "El campo guid_imagen es requerido" });
                }

                // Validar formato de GUID
                if (!Guid.TryParse(request.guid_imagen, out _))
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { error = "El guid_imagen no tiene un formato válido" });
                }

                // Preparar JSON para enviar a BD
                var jsonEntrada = new
                {
                    guid_imagen = request.guid_imagen
                };

                string jsonDatosImagen = System.Text.Json.JsonSerializer.Serialize(jsonEntrada);
                
                LogHelper.RegistrarLog($"JSON Entrada a BD (Eliminar): {jsonDatosImagen}");

                // Llamar al procedimiento almacenado
                Operacion.EsValidacionEnOrapro = true;
                string jsonResultadoBD = Operacion.Ejecutar(
                    apiKey,
                    direccionIP,
                    "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Eliminar_Datos_Imagen",
                    "p_json",
                    jsonDatosImagen,
                    "p_out_cursor"
                );

                LogHelper.RegistrarLog($"Respuesta BD (Eliminar): {jsonResultadoBD}");

                // Parsear respuesta de BD
                JObject resultadoBD = JObject.Parse(jsonResultadoBD);
                int exitoBD = resultadoBD["exito"]?.Value<int>() ?? 0;
                string mensajeBD = resultadoBD["mensaje"]?.Value<string>() ?? "Sin mensaje";

                if (exitoBD != 1)
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new
                    {
                        error = "Error al eliminar la imagen de la base de datos",
                        mensaje = mensajeBD,
                        detalle_bd = jsonResultadoBD
                    });
                }

                // Si la BD fue exitosa, eliminar el archivo físico
                string directorioImagenes = _configuracion["DirectorioImagenes"];
                
                if (!string.IsNullOrEmpty(directorioImagenes))
                {
                    // Buscar archivos con el GUID (cualquier extensión)
                    var archivosAEliminar = Directory.GetFiles(directorioImagenes, $"{request.guid_imagen}.*");
                    
                    foreach (var archivoPath in archivosAEliminar)
                    {
                        try
                        {
                            if (System.IO.File.Exists(archivoPath))
                            {
                                System.IO.File.Delete(archivoPath);
                                LogHelper.RegistrarLog($"Archivo físico eliminado: {Path.GetFileName(archivoPath)}");
                            }
                        }
                        catch (Exception deleteEx)
                        {
                            LogHelper.RegistrarLog($"Advertencia: No se pudo eliminar el archivo físico {Path.GetFileName(archivoPath)}: {deleteEx.Message}");
                        }
                    }
                }

                var respuesta = new
                {
                    exito = exitoBD,
                    mensaje = mensajeBD,
                    guid_imagen = request.guid_imagen
                };

                return StatusCode(StatusCodes.Status200OK, respuesta);
            }
            catch (Newtonsoft.Json.JsonException jsonEx)
            {
                var errorDetails = new
                {
                    Message = "Error al procesar la respuesta de la base de datos",
                    DetalleError = jsonEx.Message,
                    StackTrace = jsonEx.StackTrace,
                    Parameters = new
                    {
                        ApiKey = apiKey?.Substring(0, Math.Min(5, apiKey?.Length ?? 0)) + "...",
                        DireccionIP = direccionIP,
                        GuidImagen = request.guid_imagen,
                        Procedimiento = "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Eliminar_Datos_Imagen"
                    }
                };

                LogHelper.RegistrarLog(System.Text.Json.JsonSerializer.Serialize(errorDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Error al procesar respuesta de la base de datos",
                    details = jsonEx.Message
                });
            }
            catch (Exception excepcion)
            {
                var errorDetails = new
                {
                    Message = excepcion.Message,
                    StackTrace = excepcion.StackTrace,
                    InnerException = excepcion.InnerException?.Message,
                    Source = excepcion.Source,
                    Parameters = new
                    {
                        ApiKey = apiKey?.Substring(0, Math.Min(5, apiKey?.Length ?? 0)) + "...",
                        DireccionIP = direccionIP,
                        GuidImagen = request.guid_imagen,
                        Procedimiento = "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Eliminar_Datos_Imagen"
                    }
                };

                LogHelper.RegistrarLog(System.Text.Json.JsonSerializer.Serialize(errorDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                return StatusCode(StatusCodes.Status400BadRequest, new { error = excepcion.Message, details = excepcion.ToString() });
            }
        }


        /// <summary>Crea un banner con una imagen y retorna su información.</summary>
        /// <response code="200">Banner creado exitosamente</response>
        /// <response code="400">Error en la creación</response>
        [HttpPost("/CrearBanner")]
        [SwaggerOperation("CrearBanner")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CrearBanner([FromForm] CrearBannerRequest request)
        {
            string apiKey = ControllerContext.HttpContext.Request.Headers["ApiKey"];
            string direccionIP = ControllerContext.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            string rutaCompleta = string.Empty;
            Guid guidImagen = Guid.NewGuid();

            try
            {
                // Validar que se haya enviado un archivo
                if (request.Archivo == null || request.Archivo.Length == 0)
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { error = "No se ha seleccionado ningún archivo" });
                }

                // Validar que sea una imagen
                var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(request.Archivo.FileName).ToLowerInvariant();

                if (!extensionesPermitidas.Contains(extension))
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { error = "El archivo debe ser una imagen (jpg, jpeg, png, webp)" });
                }

                // Validar campos adicionales del banner
                if (string.IsNullOrEmpty(request.NombreBanner))
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { error = "El campo nombre_banner es requerido" });
                }

                // Obtener el directorio de configuración
                string directorioImagenes = _configuracion["DirectorioImagenes"];
                string direccionPublica = _configuracion["DireccionImagenesPublica"];

                if (string.IsNullOrEmpty(directorioImagenes))
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new { error = "No se ha configurado el directorio de imágenes en appsettings.json" });
                }

                // Crear el directorio si no existe
                if (!Directory.Exists(directorioImagenes))
                {
                    Directory.CreateDirectory(directorioImagenes);
                }

                // Usar el guidImagen para el nombre del archivo
                string nombreArchivo = $"{guidImagen}{extension}";
                rutaCompleta = Path.Combine(directorioImagenes, nombreArchivo);

                // Guardar el archivo
                using (var stream = new FileStream(rutaCompleta, FileMode.Create))
                {
                    await request.Archivo.CopyToAsync(stream);
                }

                // Obtener dimensiones y peso de la imagen
                int ancho = 0;
                int alto = 0;
                long peso = new FileInfo(rutaCompleta).Length;

                // Obtener dimensiones
                var dimensiones = _imagenService.ObtenerDimensionesImagen(rutaCompleta);
                ancho = dimensiones.ancho;
                alto = dimensiones.alto;

                // Construir la URL pública
                string urlPublica = direccionPublica?.TrimEnd('/') + "/" + nombreArchivo;

                // Preparar datos del banner con los campos adicionales
                var datosBanner = new
                {
                    guid_imagen = guidImagen.ToString(),
                    url = urlPublica,
                    id_tipo_imagen = request.IdTipoImagen,
                    alt_text = request.AltText ?? Path.GetFileNameWithoutExtension(request.Archivo.FileName),
                    alto = alto,
                    ancho = ancho,
                    peso = peso,
                    version = request.Version,
                    nombre_banner = request.NombreBanner,
                    orden = request.Orden
                };

                // Serializar a JSON para enviar al procedimiento almacenado
                string jsonDatosBanner = System.Text.Json.JsonSerializer.Serialize(datosBanner);

                LogHelper.RegistrarLog($"JSON Entrada a BD (CrearBanner): {jsonDatosBanner}");

                // Registrar en la base de datos
                Operacion.EsValidacionEnOrapro = true;
                string jsonResultadoBD = Operacion.Ejecutar(
                    apiKey,
                    direccionIP,
                    "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Crear_Banner",
                    "p_json",
                    jsonDatosBanner,
                    "p_out_cursor"
                );

                LogHelper.RegistrarLog($"Banner creado exitosamente: {nombreArchivo} - URL: {urlPublica}");
                LogHelper.RegistrarLog($"Respuesta BD (CrearBanner): {jsonResultadoBD}");

                // Verificar que el registro en BD fue exitoso
                JObject resultadoBD = JObject.Parse(jsonResultadoBD);
                int exitoBD = resultadoBD["exito"]?.Value<int>() ?? 0;
                string mensajeBD = resultadoBD["mensaje"]?.Value<string>() ?? "Sin mensaje";

                if (exitoBD != 1)
                {
                    // Si hubo error en BD, eliminar el archivo físico
                    if (System.IO.File.Exists(rutaCompleta))
                    {
                        System.IO.File.Delete(rutaCompleta);
                        LogHelper.RegistrarLog($"Archivo eliminado debido a error en BD: {nombreArchivo}");
                    }

                    return StatusCode(StatusCodes.Status400BadRequest, new
                    {
                        error = "Error al crear el banner en la base de datos",
                        mensaje = mensajeBD,
                        detalle_bd = jsonResultadoBD
                    });
                }

                // Preparar respuesta con información del banner creado
                var respuesta = new
                {
                    exito = exitoBD,
                    mensaje = mensajeBD,
                    id_banner = resultadoBD["id_banner"]?.Value<int>() ?? 0,
                    guid_imagen = guidImagen.ToString(),
                    url = urlPublica,
                    id_tipo_imagen = request.IdTipoImagen,
                    alt_text = request.AltText ?? Path.GetFileNameWithoutExtension(request.Archivo.FileName),
                    alto = alto,
                    ancho = ancho,
                    peso = peso,
                    version = request.Version,
                    nombre_banner = request.NombreBanner,
                    orden = request.Orden,
                    nombre_archivo = nombreArchivo,
                    fecha_carga = DateTime.Now,
                    registrado_bd = true
                };

                return StatusCode(StatusCodes.Status200OK, respuesta);
            }
            catch (Newtonsoft.Json.JsonException jsonEx)
            {
                // Error al parsear la respuesta de la BD - eliminar archivo
                if (!string.IsNullOrEmpty(rutaCompleta) && System.IO.File.Exists(rutaCompleta))
                {
                    System.IO.File.Delete(rutaCompleta);
                    LogHelper.RegistrarLog($"Archivo eliminado debido a error de JSON en respuesta BD: {rutaCompleta}");
                }

                var errorDetails = new
                {
                    Message = "Error al procesar la respuesta de la base de datos",
                    DetalleError = jsonEx.Message,
                    StackTrace = jsonEx.StackTrace,
                    Parameters = new
                    {
                        ApiKey = apiKey?.Substring(0, Math.Min(5, apiKey?.Length ?? 0)) + "...",
                        DireccionIP = direccionIP,
                        NombreArchivo = request?.Archivo?.FileName,
                        GuidImagen = guidImagen.ToString(),
                        NombreBanner = request?.NombreBanner,
                        Orden = request?.Orden,
                        Procedimiento = "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Crear_Banner"
                    }
                };

                LogHelper.RegistrarLog(System.Text.Json.JsonSerializer.Serialize(errorDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Error al procesar respuesta de la base de datos",
                    details = jsonEx.Message
                });
            }
            catch (Exception excepcion)
            {
                // Error general - eliminar archivo si existe
                if (!string.IsNullOrEmpty(rutaCompleta) && System.IO.File.Exists(rutaCompleta))
                {
                    try
                    {
                        System.IO.File.Delete(rutaCompleta);
                        LogHelper.RegistrarLog($"Archivo eliminado debido a excepción: {rutaCompleta}");
                    }
                    catch (Exception deleteEx)
                    {
                        LogHelper.RegistrarLog($"Error al eliminar archivo: {deleteEx.Message}");
                    }
                }

                var errorDetails = new
                {
                    Message = excepcion.Message,
                    StackTrace = excepcion.StackTrace,
                    InnerException = excepcion.InnerException?.Message,
                    Source = excepcion.Source,
                    Parameters = new
                    {
                        ApiKey = apiKey?.Substring(0, Math.Min(5, apiKey?.Length ?? 0)) + "...",
                        DireccionIP = direccionIP,
                        NombreArchivo = request?.Archivo?.FileName,
                        IdTipoImagen = request?.IdTipoImagen,
                        GuidImagen = guidImagen.ToString(),
                        NombreBanner = request?.NombreBanner,
                        Orden = request?.Orden,
                        Procedimiento = "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Crear_Banner"
                    }
                };

                LogHelper.RegistrarLog(System.Text.Json.JsonSerializer.Serialize(errorDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                return StatusCode(StatusCodes.Status400BadRequest, new { error = excepcion.Message, details = excepcion.ToString() });
            }
        }


        /// <summary>Obtiene todos los banners registrados.</summary>
        /// <response code="200">Consulta exitosa</response>
        /// <response code="400">Error al obtener banners.</response>        
        [HttpGet("/ObtenerBanners")]
        [SwaggerOperation("ObtenerBanners")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
        public IActionResult ObtenerBanners()
        {
            IActionResult actionResult = StatusCode(StatusCodes.Status400BadRequest);
            string jsonResultado = string.Empty;
            string apiKey = ControllerContext.HttpContext.Request.Headers["ApiKey"];
            string direccionIP = ControllerContext.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            try
            {
                apiKey = apiKey != null ? apiKey : "";

                Operacion.EsValidacionEnOrapro = true;

                jsonResultado = Operacion.Ejecutar(apiKey, direccionIP, "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Obtener_Banners", "p_out_cursor");

                LogHelper.RegistrarLog($"Respuesta BD (ObtenerBanners): {jsonResultado}");

                int codigoEstatus = ResponseHelper.ObtenerCodigoEstatusRespuesta(jsonResultado, StatusCodes.Status200OK);

                return StatusCode(codigoEstatus, jsonResultado);
            }
            catch (Exception excepcion)
            {
                var errorDetails = new
                {
                    Message = excepcion.Message,
                    StackTrace = excepcion.StackTrace,
                    InnerException = excepcion.InnerException?.Message,
                    Source = excepcion.Source,
                    RawResponse = jsonResultado,
                    Parameters = new
                    {
                        NombreConexion = _configuracion["nombreConexion"],
                        ApiKey = apiKey?.Substring(0, Math.Min(5, apiKey?.Length ?? 0)) + "...",
                        DireccionIP = direccionIP,
                        Procedure = "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Obtener_Banners"
                    }
                };

                LogHelper.RegistrarLog(System.Text.Json.JsonSerializer.Serialize(errorDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                return StatusCode(StatusCodes.Status400BadRequest, new { error = excepcion.Message, details = excepcion.ToString() });
            }
        }

        /// <summary>Elimina un banner de la base de datos. Elimina solo la infrmación del banner y su relacion con una imagen. NO ELIMINA IMAGEN</summary>
        /// <response code="200">Banner eliminado exitosamente</response>
        /// <response code="400">Error en la eliminación</response>
        [HttpPost("/EliminarBanner")]
        [SwaggerOperation("EliminarBanner")]
        [SwaggerResponse(StatusCodes.Status200OK)]
        [SwaggerResponse(StatusCodes.Status400BadRequest)]
        public IActionResult EliminarBanner([FromBody] EliminarBannerRequest request)
        {
            string apiKey = ControllerContext.HttpContext.Request.Headers["ApiKey"];
            string direccionIP = ControllerContext.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            try
            {
                // Validar que se haya enviado el id_banner
                if (request.id_banner <= 0)
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new { error = "El campo id_banner es requerido y debe ser mayor a 0" });
                }

                // Preparar JSON para enviar a BD
                var jsonEntrada = new
                {
                    id_banner = request.id_banner.ToString()
                };

                string jsonDatosBanner = System.Text.Json.JsonSerializer.Serialize(jsonEntrada);
                
                LogHelper.RegistrarLog($"JSON Entrada a BD (EliminarBanner): {jsonDatosBanner}");

                // Llamar al procedimiento almacenado
                Operacion.EsValidacionEnOrapro = true;
                string jsonResultadoBD = Operacion.Ejecutar(
                    apiKey,
                    direccionIP,
                    "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Eliminar_Banner",
                    "p_json",
                    jsonDatosBanner,
                    "p_out_cursor"
                );

                LogHelper.RegistrarLog($"Respuesta BD (EliminarBanner): {jsonResultadoBD}");

                // Parsear respuesta de BD
                JObject resultadoBD = JObject.Parse(jsonResultadoBD);
                int exitoBD = resultadoBD["exito"]?.Value<int>() ?? 0;
                string mensajeBD = resultadoBD["mensaje"]?.Value<string>() ?? "Sin mensaje";
                int idBannerEliminado = resultadoBD["id_banner"]?.Value<int>() ?? 0;

                if (exitoBD != 1)
                {
                    return StatusCode(StatusCodes.Status400BadRequest, new
                    {
                        error = "Error al eliminar el banner de la base de datos",
                        mensaje = mensajeBD,
                        detalle_bd = jsonResultadoBD
                    });
                }

                var respuesta = new
                {
                    exito = exitoBD,
                    mensaje = mensajeBD,
                    id_banner = idBannerEliminado
                };

                return StatusCode(StatusCodes.Status200OK, respuesta);
            }
            catch (Newtonsoft.Json.JsonException jsonEx)
            {
                var errorDetails = new
                {
                    Message = "Error al procesar la respuesta de la base de datos",
                    DetalleError = jsonEx.Message,
                    StackTrace = jsonEx.StackTrace,
                    Parameters = new
                    {
                        ApiKey = apiKey?.Substring(0, Math.Min(5, apiKey?.Length ?? 0)) + "...",
                        DireccionIP = direccionIP,
                        IdBanner = request.id_banner,
                        Procedimiento = "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Eliminar_Banner"
                    }
                };

                LogHelper.RegistrarLog(System.Text.Json.JsonSerializer.Serialize(errorDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    error = "Error al procesar respuesta de la base de datos",
                    details = jsonEx.Message
                });
            }
            catch (Exception excepcion)
            {
                var errorDetails = new
                {
                    Message = excepcion.Message,
                    StackTrace = excepcion.StackTrace,
                    InnerException = excepcion.InnerException?.Message,
                    Source = excepcion.Source,
                    Parameters = new
                    {
                        ApiKey = apiKey?.Substring(0, Math.Min(5, apiKey?.Length ?? 0)) + "...",
                        DireccionIP = direccionIP,
                        IdBanner = request.id_banner,
                        Procedimiento = "SVMOVIL.PCK_AC_CONTROL_IMAGENES.Eliminar_Banner"
                    }
                };

                LogHelper.RegistrarLog(System.Text.Json.JsonSerializer.Serialize(errorDetails, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

                return StatusCode(StatusCodes.Status400BadRequest, new { error = excepcion.Message, details = excepcion.ToString() });
            }
        }

    }
}
                           

