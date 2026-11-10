import React, { useEffect, useRef } from 'react';
import * as THREE from 'three';

export type AgentVisualState = 'IDLE' | 'LISTENING' | 'THINKING' | 'SPEAKING' | 'EXECUTING';

interface HologramCoreCanvasProps {
  state: AgentVisualState;
  audioLevel?: number; // 0.0 to 1.0 from microphone or TTS
}

export const HologramCoreCanvas: React.FC<HologramCoreCanvasProps> = ({ state, audioLevel = 0 }) => {
  const containerRef = useRef<HTMLDivElement>(null);
  const stateRef = useRef(state);
  const audioLevelRef = useRef(audioLevel);

  useEffect(() => {
    stateRef.current = state;
  }, [state]);

  useEffect(() => {
    audioLevelRef.current = audioLevel;
  }, [audioLevel]);

  useEffect(() => {
    const container = containerRef.current;
    if (!container) return;

    const width = container.clientWidth;
    const height = container.clientHeight;

    // Scene, Camera, Renderer
    const scene = new THREE.Scene();
    const camera = new THREE.PerspectiveCamera(45, width / height, 0.1, 1000);
    camera.position.z = 5.2;

    const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: 'high-performance' });
    renderer.setSize(width, height);
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    container.appendChild(renderer.domElement);

    // 1. Fibonacci Sphere Lattice of Delicate White Points (Matheus Tilli JARVIS Aesthetic)
    const numPoints = 1600;
    const baseRadius = 1.65;
    const positions = new Float32Array(numPoints * 3);
    const originalPositions = new Float32Array(numPoints * 3);
    const pointPhis = new Float32Array(numPoints);
    const pointThetas = new Float32Array(numPoints);

    const goldenRatio = (1 + Math.sqrt(5)) / 2;

    for (let i = 0; i < numPoints; i++) {
      // Golden Spiral / Fibonacci sphere distribution
      const theta = 2 * Math.PI * i / goldenRatio;
      const phi = Math.acos(1 - 2 * (i + 0.5) / numPoints);

      const x = baseRadius * Math.sin(phi) * Math.cos(theta);
      const y = baseRadius * Math.sin(phi) * Math.sin(theta);
      const z = baseRadius * Math.cos(phi);

      positions[i * 3] = x;
      positions[i * 3 + 1] = y;
      positions[i * 3 + 2] = z;

      originalPositions[i * 3] = x;
      originalPositions[i * 3 + 1] = y;
      originalPositions[i * 3 + 2] = z;

      pointPhis[i] = phi;
      pointThetas[i] = theta;
    }

    const geometry = new THREE.BufferGeometry();
    geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));

    // Create crisp round circular texture for points
    const canvas = document.createElement('canvas');
    canvas.width = 64;
    canvas.height = 64;
    const ctx = canvas.getContext('2d');
    if (ctx) {
      const gradient = ctx.createRadialGradient(32, 32, 0, 32, 32, 30);
      gradient.addColorStop(0, 'rgba(255, 255, 255, 1)');
      gradient.addColorStop(0.3, 'rgba(240, 250, 255, 0.9)');
      gradient.addColorStop(0.65, 'rgba(0, 240, 255, 0.45)');
      gradient.addColorStop(1, 'rgba(0, 240, 255, 0)');
      ctx.fillStyle = gradient;
      ctx.fillRect(0, 0, 64, 64);
    }
    const pointTexture = new THREE.CanvasTexture(canvas);

    // Delicate points material - White with subtle electric cyan aura
    const material = new THREE.PointsMaterial({
      color: 0xffffff,
      size: 0.042,
      map: pointTexture,
      transparent: true,
      opacity: 0.92,
      blending: THREE.AdditiveBlending,
      depthWrite: false
    });

    const pointCloud = new THREE.Points(geometry, material);
    scene.add(pointCloud);

    // Subtle soft inner ambient core glow
    const innerGlowGeo = new THREE.SphereGeometry(1.2, 32, 32);
    const innerGlowMat = new THREE.MeshBasicMaterial({
      color: 0x004488,
      transparent: true,
      opacity: 0.08,
      blending: THREE.AdditiveBlending
    });
    const innerGlow = new THREE.Mesh(innerGlowGeo, innerGlowMat);
    scene.add(innerGlow);

    // Resize Handler
    const handleResize = () => {
      if (!container) return;
      const w = container.clientWidth;
      const h = container.clientHeight;
      camera.aspect = w / h;
      camera.updateProjectionMatrix();
      renderer.setSize(w, h);
    };
    window.addEventListener('resize', handleResize);

    // Gentle mouse parallax
    let mouseX = 0;
    let mouseY = 0;
    const handleMouseMove = (e: MouseEvent) => {
      mouseX = (e.clientX / window.innerWidth - 0.5) * 0.4;
      mouseY = (e.clientY / window.innerHeight - 0.5) * 0.4;
    };
    window.addEventListener('mousemove', handleMouseMove);

    // Animation Loop
    let animationFrameId: number;
    let clock = new THREE.Clock();

    const animate = () => {
      animationFrameId = requestAnimationFrame(animate);
      const elapsedTime = clock.getElapsedTime();
      const currentState = stateRef.current;
      const currentAudio = audioLevelRef.current;

      // Base rotation: Calm, slow, and hypnotic (does NOT spin wildly when user is silent)
      let rotationSpeedY = 0.08;
      let rotationSpeedX = 0.04;
      let waveAmplitude = 0.015; // very subtle breathing when IDLE
      let waveFrequency = 1.5;

      if (currentState === 'LISTENING') {
        rotationSpeedY = 0.12;
        waveAmplitude = 0.03 + (currentAudio * 0.38);
        waveFrequency = 3.5;
        material.color.setHex(0xe8fcff); // brighter pure white-cyan
      } else if (currentState === 'THINKING' || currentState === 'EXECUTING') {
        rotationSpeedY = 0.25;
        waveAmplitude = 0.06 + Math.sin(elapsedTime * 6) * 0.03;
        waveFrequency = 5.0;
        material.color.setHex(0xffea9f); // gold-white pulse
      } else if (currentState === 'SPEAKING') {
        rotationSpeedY = 0.14;
        waveAmplitude = 0.04 + (currentAudio * 0.45);
        waveFrequency = 4.0;
        material.color.setHex(0xffffff); // pure crisp white
      } else {
        // IDLE: peaceful and minimal
        material.color.setHex(0xf0faff);
      }

      // Rotate point cloud gently
      pointCloud.rotation.y += rotationSpeedY * 0.016;
      pointCloud.rotation.x = Math.sin(elapsedTime * 0.2) * 0.08;

      // Deform points organically
      const posAttr = geometry.attributes.position;
      const posArray = posAttr.array as Float32Array;

      for (let i = 0; i < numPoints; i++) {
        const phi = pointPhis[i];
        const theta = pointThetas[i];

        const ox = originalPositions[i * 3];
        const oy = originalPositions[i * 3 + 1];
        const oz = originalPositions[i * 3 + 2];

        // Harmonic spherical soundwave deformation
        const harmonicWave = Math.sin(theta * 4 + elapsedTime * waveFrequency) *
                             Math.cos(phi * 3 - elapsedTime * (waveFrequency * 0.8));

        const displacement = 1.0 + (harmonicWave * waveAmplitude);

        posArray[i * 3] = ox * displacement;
        posArray[i * 3 + 1] = oy * displacement;
        posArray[i * 3 + 2] = oz * displacement;
      }
      posAttr.needsUpdate = true;

      // Soft camera parallax lerp
      camera.position.x = THREE.MathUtils.lerp(camera.position.x, mouseX, 0.05);
      camera.position.y = THREE.MathUtils.lerp(camera.position.y, -mouseY, 0.05);
      camera.lookAt(0, 0, 0);

      renderer.render(scene, camera);
    };

    animate();

    return () => {
      cancelAnimationFrame(animationFrameId);
      window.removeEventListener('resize', handleResize);
      window.removeEventListener('mousemove', handleMouseMove);
      renderer.dispose();
      geometry.dispose();
      material.dispose();
      pointTexture.dispose();
      innerGlowGeo.dispose();
      innerGlowMat.dispose();
      if (container.contains(renderer.domElement)) {
        container.removeChild(renderer.domElement);
      }
    };
  }, []);

  return (
    <div
      ref={containerRef}
      style={{
        position: 'absolute',
        top: 0,
        left: 0,
        width: '100%',
        height: '100%',
        zIndex: 2,
        pointerEvents: 'none'
      }}
    />
  );
};
