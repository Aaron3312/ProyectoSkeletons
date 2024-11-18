import torch
import cv2
import numpy as np
from PIL import Image
import matplotlib.pyplot as plt

def preprocess_and_test(image_path):
    """
    Prueba múltiples preprocesamientos de la imagen con configuración mejorada
    """
    # Cargar el modelo con configuración modificada
    model = torch.hub.load('ultralytics/yolov5', 'yolov5s')
    
    # Expandir clases relevantes para incluir más objetos similares
    relevant_classes = [39, 41, 44, 75]  # bottle, cup, bowl, vase
    
    # Ajustar la confianza mínima
    model.conf = 0.25  # Reducir el umbral de confianza
    
    # Leer la imagen original
    img_original = cv2.imread(image_path)
    img_rgb = cv2.cvtColor(img_original, cv2.COLOR_BGR2RGB)
    
    # Crear visualización de debug
    plt.figure(figsize=(20, 15))
    
    # Preparar diferentes versiones de la imagen
    preprocessed_images = {
        'Original': img_rgb,
        'Contraste': cv2.convertScaleAbs(img_rgb, alpha=1.8, beta=10),  # Aumentado contraste
        'Histograma': cv2.cvtColor(
            cv2.equalizeHist(cv2.cvtColor(img_rgb, cv2.COLOR_RGB2GRAY)),
            cv2.COLOR_GRAY2RGB
        ),
        'Gamma': np.power(img_rgb/255.0, 1.8).clip(0, 1) * 255.0,  # Gamma más alto
        'Sharpen': cv2.filter2D(img_rgb, -1, np.array([[-1,-1,-1], [-1,9,-1], [-1,-1,-1]])),
        'Red Enhanced': img_rgb.copy()
    }
    
    # Mejorar canal rojo para Coca-Cola
    preprocessed_images['Red Enhanced'][:,:,0] = cv2.convertScaleAbs(
        preprocessed_images['Red Enhanced'][:,:,0], 
        alpha=1.5, 
        beta=10
    )
    
    # Mostrar todas las versiones
    for idx, (name, img) in enumerate(preprocessed_images.items(), 1):
        plt.subplot(2, 3, idx)
        plt.imshow(img.astype(np.uint8))
        plt.title(name)
    
    # Guardar visualización
    plt.savefig('preprocessing_debug.png')
    plt.close()
    
    best_confidence = 0
    best_detection = None
    best_version = None
    
    print("\nProbando diferentes preprocesamientos:")
    print("-" * 50)
    
    for version_name, img in preprocessed_images.items():
        print(f"\nProbando versión: {version_name}")
        
        # Normalizar imagen
        img = img.astype(np.uint8)
        
        # Probar detección con diferentes tamaños
        scales = [1.0, 1.5, 0.75]  # Probar diferentes escalas
        for scale in scales:
            if scale != 1.0:
                height, width = img.shape[:2]
                new_height, new_width = int(height * scale), int(width * scale)
                scaled_img = cv2.resize(img, (new_width, new_height))
            else:
                scaled_img = img
                
            # Probar con diferentes clases
            for class_id in relevant_classes:
                model.classes = [class_id]
                results = model(scaled_img)
                detections = results.pandas().xyxy[0]
                
                if not detections.empty:
                    max_conf = detections['confidence'].max()
                    if max_conf > best_confidence:
                        best_confidence = max_conf
                        best_detection = detections
                        best_version = f"{version_name} (scale: {scale})"
                    
                    print(f"Clase {class_id} - Escala {scale} - Detecciones:")
                    print(detections)
                    
                    # Guardar imagen con detecciones
                    results.render()
                    output_path = f'detection_{version_name}_class{class_id}_scale{scale}.png'
                    Image.fromarray(results.ims[0]).save(output_path)
    
    if best_detection is not None:
        print("\nMejor detección encontrada:")
        print(f"Versión: {best_version}")
        print(f"Confianza: {best_confidence}")
        print(best_detection)
    else:
        print("\nNo se encontraron detecciones en ninguna versión")
        
        # Análisis detallado de la imagen
        print("\nAnálisis de la imagen:")
        print("1. Dimensiones:", img_rgb.shape)
        print("2. Estadísticas de color (RGB):")
        for i, color in enumerate(['Rojo', 'Verde', 'Azul']):
            values = img_rgb[:,:,i].ravel()
            print(f"   {color}:")
            print(f"      min={values.min()}, max={values.max()}")
            print(f"      mean={values.mean():.2f}, std={values.std():.2f}")
            print(f"      median={np.median(values):.2f}")

def main():
    image_path = 'vision_debug/agent0_detection_20241114_161634_025906.png'
    preprocess_and_test(image_path)

if __name__ == "__main__":
    main()