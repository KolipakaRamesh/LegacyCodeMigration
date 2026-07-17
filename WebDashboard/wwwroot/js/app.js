document.addEventListener('DOMContentLoaded', () => {
    // Buttons & Loaders
    const generateBtn = document.getElementById('generate-btn');
    const btnSpinner = document.getElementById('btn-spinner');
    const messageEl = document.getElementById('generation-message');

    // Migration Elements
    const migrateBtn = document.getElementById('migrate-btn');
    const migrateSpinner = document.getElementById('migrate-spinner');
    const migrationMessageEl = document.getElementById('migration-message');
    const swaggerContainer = document.getElementById('swagger-container');
    const swaggerLink = document.getElementById('swagger-link');
    const deleteApiBtn = document.getElementById('delete-api-btn');
    const deleteApiSpinner = document.getElementById('delete-api-spinner');

    // File info
    const nodesBadge = document.getElementById('nodes-badge');
    const nodesPath = document.getElementById('nodes-path');
    const nodesMeta = document.getElementById('nodes-meta');
    const nodesSize = document.getElementById('nodes-size');
    const nodesTime = document.getElementById('nodes-time');

    const relsBadge = document.getElementById('rels-badge');
    const relsPath = document.getElementById('rels-path');
    const relsMeta = document.getElementById('rels-meta');
    const relsSize = document.getElementById('rels-size');
    const relsTime = document.getElementById('rels-time');

    // Stats
    const stats = {
        totalNodes: document.getElementById('stat-total-nodes'),
        totalRelationships: document.getElementById('stat-total-relationships'),
        namespaces: document.getElementById('stat-namespaces'),
        classes: document.getElementById('stat-classes'),
        interfaces: document.getElementById('stat-interfaces'),
        methods: document.getElementById('stat-methods'),
        properties: document.getElementById('stat-properties'),
        fields: document.getElementById('stat-fields'),
        constructors: document.getElementById('stat-constructors'),
        enums: document.getElementById('stat-enums')
    };

    // Initialize Page State
    checkExistingFiles();

    // Initialize Graph Visualizer Controls
    if (window.CodeGraphVisualizer) {
        window.CodeGraphVisualizer.init();
    }

    // Event Listeners
    generateBtn.addEventListener('click', generateGraph);
    migrateBtn.addEventListener('click', migrateToWebApi);
    deleteApiBtn.addEventListener('click', deleteGeneratedApi);

    // Functions
    async function checkExistingFiles() {
        try {
            const response = await fetch('/api/graph/files');
            const data = await response.json();

            if (data.generated && data.files) {
                updateFileUI(data.files);
                migrateBtn.disabled = false;
                if (data.apiMigrated) {
                    deleteApiBtn.style.display = 'flex';
                } else {
                    deleteApiBtn.style.display = 'none';
                }
                if (data.statistics) {
                    updateStatsUI(data.statistics);
                } else {
                    // If files exist but no stats were returned on initial load, trigger a silent run or fetch
                    silentRefreshStats();
                }
            }
        } catch (error) {
            console.error('Error checking existing files:', error);
        }
    }

    async function silentRefreshStats() {
        try {
            // Trigger a quick fetch of generate to populate stats silently if files are already on disk
            const response = await fetch('/api/graph/generate', { method: 'POST' });
            const data = await response.json();
            if (data.success) {
                updateStatsUI(data.statistics);
                updateFileUI(data.files);
                migrateBtn.disabled = false;
                if (data.apiMigrated) {
                    deleteApiBtn.style.display = 'flex';
                } else {
                    deleteApiBtn.style.display = 'none';
                }
                if (window.CodeGraphVisualizer) {
                    window.CodeGraphVisualizer.refresh();
                }
            }
        } catch (e) {
            console.warn('Silent stats refresh failed', e);
        }
    }

    async function generateGraph() {
        // Update UI to generating state
        generateBtn.disabled = true;
        btnSpinner.classList.remove('hidden');
        messageEl.className = 'message hidden';

        // Reset Stats visually to indicate reload
        Object.values(stats).forEach(el => el.classList.remove('glow-num'));

        try {
            const response = await fetch('/api/graph/generate', {
                method: 'POST'
            });

            if (!response.ok) {
                const errData = await response.json();
                throw new Error(errData.detail || 'Failed to generate graph files.');
            }

            const data = await response.json();

            if (data.success) {
                updateFileUI(data.files);
                updateStatsUI(data.statistics);
                migrateBtn.disabled = false;

                messageEl.textContent = 'Knowledge graph compiled and exported successfully!';
                messageEl.className = 'message success';

                if (window.CodeGraphVisualizer) {
                    window.CodeGraphVisualizer.refresh();
                }
            }
        } catch (error) {
            console.error('Generation error:', error);
            messageEl.textContent = `Error: ${error.message}`;
            messageEl.className = 'message error';
        } finally {
            generateBtn.disabled = false;
            btnSpinner.classList.add('hidden');
        }
    }

    async function migrateToWebApi() {
        migrateBtn.disabled = true;
        migrateSpinner.classList.remove('hidden');
        migrationMessageEl.className = 'message hidden';
        swaggerContainer.classList.add('hidden');

        try {
            const response = await fetch('/api/graph/migrate', {
                method: 'POST'
            });

            if (!response.ok) {
                const errData = await response.json();
                throw new Error(errData.detail || 'Failed to scaffold and run Web API project.');
            }

            const data = await response.json();

            if (data.success) {
                migrationMessageEl.textContent = 'Web API scaffolded, built, and launched successfully!';
                migrationMessageEl.className = 'message success';
                swaggerLink.href = data.swaggerUrl;
                swaggerContainer.classList.remove('hidden');
                deleteApiBtn.style.display = 'flex';
            }
        } catch (error) {
            console.error('Migration error:', error);
            migrationMessageEl.textContent = `Error: ${error.message}`;
            migrationMessageEl.className = 'message error';
        } finally {
            migrateBtn.disabled = false;
            migrateSpinner.classList.add('hidden');
        }
    }

    async function deleteGeneratedApi() {
        deleteApiBtn.disabled = true;
        deleteApiSpinner.classList.remove('hidden');
        migrationMessageEl.className = 'message hidden';

        try {
            const response = await fetch('/api/graph/delete-api', {
                method: 'POST'
            });

            if (!response.ok) {
                const errData = await response.json();
                throw new Error(errData.detail || 'Failed to delete Web API project.');
            }

            const data = await response.json();

            if (data.success) {
                migrationMessageEl.textContent = 'Web API project directory deleted successfully!';
                migrationMessageEl.className = 'message success';
                swaggerContainer.classList.add('hidden');
                deleteApiBtn.style.display = 'none';
            }
        } catch (error) {
            console.error('Delete error:', error);
            migrationMessageEl.textContent = `Error: ${error.message}`;
            migrationMessageEl.className = 'message error';
        } finally {
            deleteApiBtn.disabled = false;
            deleteApiSpinner.classList.add('hidden');
        }
    }

    function updateFileUI(files) {
        files.forEach(file => {
            const isNodes = file.name === 'nodes.json';
            const badge = isNodes ? nodesBadge : relsBadge;
            const pathEl = isNodes ? nodesPath : relsPath;
            const metaEl = isNodes ? nodesMeta : relsMeta;
            const sizeEl = isNodes ? nodesSize : relsSize;
            const timeEl = isNodes ? nodesTime : relsTime;
            const linkEl = document.getElementById(isNodes ? 'nodes-link' : 'rels-link');

            // Update badge style
            badge.textContent = 'Generated ✓';
            badge.className = 'badge badge-success';

            // Enable link
            if (linkEl) {
                linkEl.classList.add('active-link');
            }

            // Show details
            pathEl.textContent = file.path;
            sizeEl.textContent = formatBytes(file.sizeBytes);
            timeEl.textContent = formatDateTime(file.generatedAt);
            metaEl.classList.remove('hidden');
        });
    }

    function updateStatsUI(metrics) {
        const mapping = {
            totalNodes: metrics.totalNodes,
            totalRelationships: metrics.totalRelationships,
            namespaces: metrics.namespaceCount,
            classes: metrics.classCount,
            interfaces: metrics.interfaceCount,
            methods: metrics.methodCount,
            properties: metrics.propertyCount,
            fields: metrics.fieldCount,
            constructors: metrics.constructorCount,
            enums: metrics.enumCount
        };

        for (const [key, val] of Object.entries(mapping)) {
            if (stats[key]) {
                animateCounter(stats[key], parseInt(stats[key].textContent) || 0, val);
                stats[key].classList.add('glow-num');
            }
        }
    }

    // Helper functions
    function formatBytes(bytes) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const dm = 2;
        const sizes = ['Bytes', 'KB', 'MB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
    }

    // Convert date string to formatted locale string
    function formatDateTime(dateStr) {
        const date = new Date(dateStr);
        return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' }) + ' ' + date.toLocaleDateString();
    }

    function animateCounter(element, start, end) {
        if (start === end) {
            element.textContent = end;
            return;
        }

        let current = start;
        const range = end - start;
        const duration = 800; // ms
        let startTimestamp = null;

        const step = (timestamp) => {
            if (!startTimestamp) startTimestamp = timestamp;
            const progress = Math.min((timestamp - startTimestamp) / duration, 1);
            current = Math.floor(progress * range + start);
            element.textContent = current;
            if (progress < 1) {
                window.requestAnimationFrame(step);
            } else {
                element.textContent = end;
            }
        };

        window.requestAnimationFrame(step);
    }
});
