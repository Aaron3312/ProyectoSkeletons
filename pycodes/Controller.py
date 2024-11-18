import agentpy as ap
from flask import Flask, jsonify, request
import numpy as np
import random

app = Flask(__name__)

class RobotAgent(ap.Agent):
    """A robot agent that can pick up and transport cubes"""
    
    def setup(self):
        self.position = [0, 0, 0]  # x, y, z coordinates
        self.has_cube = False
        self.target_cube = None
        self.target_position = None
        self.state = "searching"  # states: searching, moving_to_cube, picking, transporting, dropping
        
    def step(self):
        """Determine next action based on current state"""
        actions = []
        
        if self.state == "searching" and not self.has_cube:
            # Find nearest available cube
            available_cubes = [cube for cube in self.model.cubes if not cube['being_carried']]
            if available_cubes:
                self.target_cube = random.choice(available_cubes)
                self.target_position = self.target_cube['position']
                self.state = "moving_to_cube"
                actions.append({"type": "move", "agent_id": self.id, 
                              "target": self.target_position})
                
        elif self.state == "moving_to_cube":
            # Check if we've reached the cube
            if self.position == self.target_position:
                self.state = "picking"
                actions.append({"type": "pick", "agent_id": self.id, 
                              "cube_id": self.target_cube['id']})
                self.has_cube = True
                self.target_cube['being_carried'] = True
                # Choose random drop point
                self.target_position = [random.randint(-10, 10), 0, random.randint(-10, 10)]
                self.state = "transporting"
            
        elif self.state == "transporting":
            if self.position == self.target_position:
                self.state = "dropping"
                actions.append({"type": "drop", "agent_id": self.id, 
                              "cube_id": self.target_cube['id']})
                self.has_cube = False
                self.target_cube['being_carried'] = False
                self.target_cube['position'] = self.position
                self.state = "searching"
            else:
                actions.append({"type": "move", "agent_id": self.id, 
                              "target": self.target_position})
                
        return actions

class RobotWorld(ap.Model):
    """A model of robots moving cubes around"""
    
    def setup(self):
        self.actions = []
        self.agents = ap.AgentList(self, self.p.num_robots, RobotAgent)
        
        # Initialize cubes with random positions
        self.cubes = []
        for i in range(self.p.num_cubes):
            self.cubes.append({
                'id': i,
                'position': [random.randint(-10, 10), 0, random.randint(-10, 10)],
                'being_carried': False
            })

    def step(self):
        self.actions = []
        for agent in self.agents:
            self.actions.extend(agent.step())

    def update(self):
        if self.t >= self._steps:
            self.stop()

# Global model instance
parameters = {
    'num_robots': 5,
    'num_cubes': 10,
    'steps': 1000
}
model = RobotWorld(parameters)
model.sim_setup()

@app.route('/setup', methods=['GET'])
def setup():
    """Initial setup information for Unity"""
    return jsonify({
        'robots': model.p.num_robots,
        'cubes': model.cubes
    })

@app.route('/step', methods=['POST'])
def step():
    """Process updates from Unity and return next actions"""
    # Update positions from Unity
    positions = eval(request.form['positions'])
    for i, pos in enumerate(positions):
        model.agents[i].position = pos
    
    # Execute model step
    model.sim_step()
    model.update()
    
    return jsonify({'actions': model.actions})

if __name__ == '__main__':
    app.run(debug=True)