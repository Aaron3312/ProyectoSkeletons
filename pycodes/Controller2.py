import agentpy as ap
from flask import Flask, jsonify, request
from flask_cors import CORS
from datetime import datetime
import logging
import math

# Configure logging
logging.basicConfig(level=logging.DEBUG)
logger = logging.getLogger(__name__)

app = Flask(__name__)
CORS(app)

class RobotAgent(ap.Agent):
    def setup(self):
        self.id = None
        self.has_cube = False
        self.target_cube_id = None
        self.delivery_zone = {'x': 30, 'y': 0, 'z': 0}
        self.cubes_delivered = 0
        self.start_time = datetime.now()
        self.total_distance_traveled = 0
        self.last_position = None
        self.is_delivering = False
        self.last_movement_time = None
        self.MIN_MOVEMENT_THRESHOLD = 1  # Threshold for meaningful movement
        self.last_cube_count = 0  # For rate calculation
        self.current_action = None  # Nuevo: para mantener la acción actual
        self.target_position = None  # Nuevo: para mantener el objetivo actual


    def calculate_distance(self, pos1, pos2):
        distance = math.sqrt(
            (pos1['x'] - pos2['x'])**2 +
            (pos1['y'] - pos2['y'])**2 +
            (pos1['z'] - pos2['z'])**2
        )
        return distance if distance >= self.MIN_MOVEMENT_THRESHOLD else 0

    def update_metrics(self, current_position, current_time):
        if self.start_time is None:
            self.start_time = current_time

        if self.last_position and self.last_movement_time:
            if isinstance(current_time, datetime):
                time_diff = (current_time - self.last_movement_time).total_seconds()
            else:  # Assume float (timestamp)
                time_diff = current_time - self.last_movement_time

            if time_diff >= 0.1:  # At least 100ms between updates
                distance = self.calculate_distance(current_position, self.last_position)
                if distance > 0:
                    self.total_distance_traveled += distance
                    logger.debug(f"Agent {self.id} traveled {distance} units.")
                self.last_movement_time = current_time
        else:
            self.last_movement_time = current_time

        self.last_position = current_position.copy()

    def get_utility_metrics(self, current_time):
        if self.start_time is None:
            self.start_time = current_time

        if isinstance(current_time, datetime):
            elapsed_time = (current_time - self.start_time).total_seconds()
            logger.debug(f"Elapsed time: {elapsed_time}, current_time: {current_time}, start_time: {self.start_time}")
        else:  # Assume float (timestamp)
            startTime = self.start_time.timestamp() if isinstance(self.start_time, datetime) else self.start_time


            elapsed_time = current_time - startTime
            logger.debug(f"float Elapsed time: {elapsed_time}, current_time: {current_time}, start_time: {self.start_time}")


        minutes_elapsed = elapsed_time / 60
        rate = (self.cubes_delivered) / (minutes_elapsed or 1)
        self.last_cube_count = self.cubes_delivered
        logger.debug(f"Agent {self.id} metrics: {self.cubes_delivered} cubes delivered in {minutes_elapsed} minutes. Rate: {rate}, cubecount: {self.last_cube_count}")

        return {
            'agent_id': self.id,
            'cubes_delivered': self.cubes_delivered,
            'total_distance': round(self.total_distance_traveled, 2),
            'efficiency_ratio': round(self.cubes_delivered / max(1, self.total_distance_traveled) * 10000, 2),
            'delivery_rate': round(rate, 2),
            'elapsed_minutes': round(minutes_elapsed, 2),
        }

    def find_nearest_cube(self, position, available_cubes):
        if not available_cubes:
            return None

        nearest_cube = None
        min_distance = float('inf')

        for cube in available_cubes:
            if hasattr(cube, 'targeted_by') and cube.get('targeted_by') != self.id:
                continue

            distance = self.calculate_distance(position, cube['position'])
            if distance < min_distance:
                min_distance = distance
                nearest_cube = cube

        if nearest_cube:
            nearest_cube['targeted_by'] = self.id
            self.target_cube_id = nearest_cube['id']
            logger.debug(f"Agent {self.id} targeting cube {nearest_cube['id']} at distance {min_distance}")
        else:
            logger.debug(f"Agent {self.id} found no available cubes")

        return nearest_cube

    def step(self, current_state):
        position = current_state['position']
        has_cube = current_state['has_cube']
        available_cubes = current_state['available_cubes']
        current_time = current_state.get('time', 0)

        self.update_metrics(position, current_time)

        if has_cube != self.has_cube:
            self.has_cube = has_cube
            if not has_cube and self.is_delivering:
                self.cubes_delivered += 1
                logger.debug(f"Agent {self.id} completed delivery. Total deliveries: {self.cubes_delivered}")
                self.is_delivering = False
                self.current_action = None  # Reset current action
                self.target_position = None  # Reset target position
            
        # Si ya tenemos una acción en curso y las condiciones no han cambiado, mantenerla
        if self.current_action and self.target_position:
            if self.current_action == "get_cube":
                # Verificar si el cubo objetivo aún está disponible
                target_still_available = any(cube['id'] == self.target_cube_id for cube in available_cubes)
                if target_still_available and not has_cube:
                    return {"decision": self.current_action, "target_cube": self.find_cube_by_id(available_cubes, self.target_cube_id)}
            elif self.current_action == "deliver_cube" and has_cube:
                distance_to_delivery = self.calculate_distance(position, self.delivery_zone)
                if distance_to_delivery > 2:
                    return {"decision": "deliver_cube", "target_position": self.delivery_zone}
                else:
                    return {"decision": "put_cube", "target": "delivery_zone"}

        # Si no hay acción en curso o las condiciones cambiaron, decidir nueva acción
        self.current_action = None
        self.target_position = None

        if has_cube or (self.target_cube_id is not None and not any(cube['id'] == self.target_cube_id for cube in available_cubes)):
            self.target_cube_id = None

        if not has_cube and available_cubes:
            target_cube = self.find_nearest_cube(position, available_cubes)
            if target_cube:
                self.current_action = "get_cube"
                self.target_position = target_cube['position']
                return {"decision": "get_cube", "target_cube": target_cube}
        elif has_cube:
            distance_to_delivery = self.calculate_distance(position, self.delivery_zone)
            if distance_to_delivery > 2:
                self.current_action = "deliver_cube"
                self.target_position = self.delivery_zone
                self.is_delivering = True
                return {"decision": "deliver_cube", "target_position": self.delivery_zone}
            else:
                return {"decision": "put_cube", "target": "delivery_zone"}

        return {"decision": "explore"}

    def find_cube_by_id(self, available_cubes, cube_id):
        for cube in available_cubes:
            if cube['id'] == cube_id:
                return cube
        return None


class RobotWorld(ap.Model):
    def setup(self):
        self.agents = ap.AgentList(self, self.p.num_robots, RobotAgent)
        logger.info(f"Created model with {self.p.num_robots} agents")

    def get_decisions(self, world_state):
        decisions = []
        agent_states = {entry['id']: entry['state'] for entry in world_state['agentStates']}
        while len(self.agents) < len(agent_states):
            new_agent = RobotAgent(self)
            self.agents.append(new_agent)

        for i, agent_id in enumerate(agent_states.keys()):
            self.agents[i].id = int(agent_id)

        for agent in self.agents:
            if str(agent.id) in agent_states:
                agent_state = agent_states[str(agent.id)]
                decisions.append(agent.step(agent_state))
        return decisions

    def get_metrics(self, world_state):
        metrics = []
        agent_states = {entry['id']: entry['state'] for entry in world_state['agentStates']}
        for agent in self.agents:
            if str(agent.id) in agent_states:
                agent_state = agent_states[str(agent.id)]
                agent.update_metrics(agent_state['position'], agent_state.get('time', 0))
                metrics.append(agent.get_utility_metrics(agent_state.get('time', 0)))
        logger.debug(f"Metrics: {metrics}")
        return metrics


# Flask App and Endpoints
model = RobotWorld({'num_robots': 5})
model.sim_setup()

@app.route('/get_decisions', methods=['POST'])
def get_decisions():
    try:
        world_state = request.json
        decisions = model.get_decisions(world_state)
        return jsonify({'decisions': decisions})
    except Exception as e:
        logger.error(f"Error processing decisions request: {str(e)}")
        return jsonify({'error': str(e)}), 500

@app.route('/get_metrics', methods=['POST'])
def get_metrics():
    try:
        world_state = request.json
        metrics = model.get_metrics(world_state)
        return jsonify({'metrics': metrics})
    except Exception as e:
        logger.error(f"Error processing metrics request: {str(e)}")
        return jsonify({'error': str(e)}), 500

if __name__ == '__main__':
    app.run(debug=True)
