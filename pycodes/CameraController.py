import cv2
import numpy as np
import socket
import threading
import struct
import time
import logging
import torch

logging.basicConfig(level=logging.DEBUG)
logger = logging.getLogger(__name__)

class AgentVisionReceiver:
    def __init__(self, num_agents=5, base_port=5123):
        self.num_agents = num_agents
        self.base_port = base_port
        self.running = True
        self.frame_buffer = {}
        self.lock = threading.Lock()
        
        # Cargar modelo YOLOv5
        logger.info("Cargando modelo YOLOv5...")
        self.model = torch.hub.load('ultralytics/yolov5', 'yolov5s')
        # Configurar el modelo para usar GPU si está disponible
        self.device = torch.device('cuda' if torch.cuda.is_available() else 'cpu')
        self.model.to(self.device)
        logger.info(f"Modelo YOLOv5 cargado en dispositivo: {self.device}")
        
        logger.info(f"Iniciando AgentVisionReceiver con {num_agents} agentes")
        
    def process_frame_yolo(self, frame):
        """
        Procesa un frame usando YOLOv5 y dibuja las detecciones
        """
        try:
            # Realizar inferencia
            results = self.model(frame)
            
            # Obtener detecciones
            detections = results.xyxy[0]  # Tensor de detecciones en formato xyxy
            
            # Dibujar detecciones
            for detection in detections:
                x1, y1, x2, y2, conf, cls = detection.cpu().numpy()
                if conf > 0.5:  # Umbral de confianza
                    # Convertir coordenadas a enteros
                    box = np.array([x1, y1, x2, y2]).astype(int)
                    # Obtener etiqueta y confianza
                    label = f"{self.model.names[int(cls)]} {conf:.2f}"
                    # Dibujar bbox
                    cv2.rectangle(frame, (box[0], box[1]), (box[2], box[3]), (0, 255, 0), 2)
                    # Dibujar etiqueta
                    cv2.putText(frame, label, (box[0], box[1]-10),
                              cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 255, 0), 2)
                    
            return frame
            
        except Exception as e:
            logger.error(f"Error en proceso YOLO: {e}")
            return frame
    
    def start_receiving(self):
        logger.info("Iniciando recepción de streams")
        
        for i in range(self.num_agents):
            receiver = threading.Thread(
                target=self._receive_stream, 
                args=(i,),
                name=f"Receiver-{i}"
            )
            receiver.daemon = True
            receiver.start()
            logger.debug(f"Hilo receptor {i} iniciado")
        
        self._display_streams()
        
    def _receive_stream(self, agent_id):
        port = self.base_port + agent_id
        logger.info(f"Iniciando recepción en puerto {port} para agente {agent_id}")
        
        sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        try:
            sock.bind(('0.0.0.0', port))
            sock.settimeout(1.0)
        except Exception as e:
            logger.error(f"Error al configurar socket para agente {agent_id}: {e}")
            return
            
        while self.running:
            try:
                data, addr = sock.recvfrom(65535)
                logger.debug(f"Datos recibidos de {addr} para agente {agent_id}, tamaño: {len(data)} bytes")
                
                if len(data) < 4:
                    logger.warning(f"Datos recibidos muy cortos: {len(data)} bytes")
                    continue
                
                received_agent_id = struct.unpack('i', data[:4])[0]
                if received_agent_id != agent_id:
                    logger.warning(f"ID de agente no coincide: esperado {agent_id}, recibido {received_agent_id}")
                    continue
                
                img_data = data[4:]
                nparr = np.frombuffer(img_data, np.uint8)
                frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
                
                if frame is None:
                    logger.warning("Usando frame de prueba debido a error de decodificación")
                    frame = np.ones((240, 320, 3), dtype=np.uint8) * 128
                    cv2.putText(frame, f"Agent {agent_id} - No Data", (10, 120),
                              cv2.FONT_HERSHEY_SIMPLEX, 0.5, (0, 0, 255), 1)
                else:
                    # Redimensionar si es necesario
                    frame = cv2.resize(frame, (320, 240))
                    # Procesar frame con YOLO
                    frame = self.process_frame_yolo(frame)
                    
                # Agregar texto informativo
                cv2.putText(frame, f"Agent {agent_id}", (10, 30),
                          cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
                
                with self.lock:
                    self.frame_buffer[agent_id] = frame.copy()
                    
            except socket.timeout:
                continue
            except Exception as e:
                logger.error(f"Error en recepción para agente {agent_id}: {e}")
                continue
                
    def _display_streams(self):
        logger.info("Iniciando visualización")
        cv2.namedWindow('Agent Vision Streams', cv2.WINDOW_NORMAL)
        
        while self.running:
            try:
                with self.lock:
                    current_frames = self.frame_buffer.copy()
                
                if current_frames:
                    rows = (self.num_agents + 2) // 3
                    cols = min(3, self.num_agents)
                    cell_height = 240
                    cell_width = 320
                    
                    grid = np.zeros((cell_height * rows, cell_width * cols, 3), 
                                  dtype=np.uint8)
                    
                    for agent_id, frame in current_frames.items():
                        i = agent_id // cols
                        j = agent_id % cols
                        try:
                            grid[i*cell_height:(i+1)*cell_height, 
                                 j*cell_width:(j+1)*cell_width] = frame
                        except Exception as e:
                            logger.error(f"Error al colocar frame en grid: {e}")
                    
                    cv2.imshow('Agent Vision Streams', grid)
                
                key = cv2.waitKey(1) & 0xFF
                if key == ord('q'):
                    logger.info("Tecla 'q' presionada, deteniendo...")
                    self.stop()
                    break
                
                time.sleep(0.01)
                
            except Exception as e:
                logger.error(f"Error en visualización: {e}")
                continue
            
    def stop(self):
        logger.info("Deteniendo AgentVisionReceiver")
        self.running = False
        cv2.destroyAllWindows()

if __name__ == "__main__":
    logger.info("Iniciando programa principal")
    receiver = AgentVisionReceiver(num_agents=5)
    receiver.start_receiving()