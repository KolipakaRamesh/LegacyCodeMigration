// Legacy Code Migration Graph Visualiser
// Powered by Cytoscape.js

(function() {
    // Global visualizer state
    let cyInstance = null;
    let allNodes = [];
    let allRelationships = [];
    let structuralNodesMap = new Map(); // Map of Class, Interface, Enum nodes
    let memberToOwnerMap = new Map(); // Member ID -> Class ID
    let classMembersMap = new Map(); // Class ID -> Array of members
    
    // UI Elements
    const elements = {
        container: document.getElementById('cy'),
        loading: document.getElementById('cy-loading'),
        search: document.getElementById('node-search'),
        resetLayoutBtn: document.getElementById('reset-layout-btn'),
        zoomInBtn: document.getElementById('zoom-in-btn'),
        zoomOutBtn: document.getElementById('zoom-out-btn'),
        fitBtn: document.getElementById('fit-btn'),
        
        // Filters
        filterUses: document.getElementById('filter-uses'),
        filterCalls: document.getElementById('filter-calls'),
        filterInherits: document.getElementById('filter-inherits'),
        filterImplements: document.getElementById('filter-implements'),
        
        // Details
        detailsEmpty: document.getElementById('details-empty'),
        detailsContent: document.getElementById('details-content'),
        detailType: document.getElementById('detail-node-type'),
        detailName: document.getElementById('detail-node-name'),
        detailNs: document.getElementById('detail-node-ns'),
        detailProps: document.getElementById('detail-node-props'),
        detailOutgoingList: document.getElementById('detail-outgoing-list'),
        detailIncomingList: document.getElementById('detail-incoming-list')
    };

    // Node Type Styling Colors (HSL theme)
    const typeColors = {
        'Namespace': '#4f46e5', // Indigo compound node border
        'Class': '#3b82f6',     // Cyan-Blue Class
        'Interface': '#f59e0b', // Amber contract
        'Enum': '#10b981'       // Emerald status/enum
    };

    // Initialize the visualizer
    function init() {
        if (!elements.container) return;

        // Register Event Listeners
        elements.resetLayoutBtn.addEventListener('click', runLayout);
        elements.zoomInBtn.addEventListener('click', () => cyInstance && cyInstance.zoom(cyInstance.zoom() * 1.2));
        elements.zoomOutBtn.addEventListener('click', () => cyInstance && cyInstance.zoom(cyInstance.zoom() * 0.8));
        elements.fitBtn.addEventListener('click', () => cyInstance && cyInstance.fit(30));

        // Filters
        [elements.filterUses, elements.filterCalls, elements.filterInherits, elements.filterImplements].forEach(cb => {
            if (cb) cb.addEventListener('change', applyFilters);
        });

        // Search Input
        elements.search.addEventListener('input', debounce(handleSearch, 250));

        // Initial Load
        loadGraphData();
    }

    // Debounce search input
    function debounce(func, wait) {
        let timeout;
        return function(...args) {
            clearTimeout(timeout);
            timeout = setTimeout(() => func.apply(this, args), wait);
        };
    }

    // Load nodes and relationships from disk
    async function loadGraphData() {
        elements.loading.classList.remove('hidden');
        try {
            // Fetch generated output files
            const [nodesRes, relsRes] = await Promise.all([
                fetch('/output/nodes.json'),
                fetch('/output/relationships.json')
            ]);

            if (!nodesRes.ok || !relsRes.ok) {
                throw new Error('Graph files have not been generated yet. Please generate them first.');
            }

            allNodes = await nodesRes.json();
            allRelationships = await relsRes.json();

            // Index data structures
            indexGraphData();

            // Render Cytoscape network
            renderGraph();
        } catch (error) {
            console.error('Error loading graph files:', error);
            elements.detailsEmpty.textContent = error.message;
        } finally {
            elements.loading.classList.add('hidden');
        }
    }

    // Parse and index nodes/relationships for fast lookup
    function indexGraphData() {
        structuralNodesMap.clear();
        memberToOwnerMap.clear();
        classMembersMap.clear();

        // 1. First Pass: Index structural types (Class, Interface, Enum)
        allNodes.forEach(node => {
            const type = node.Type;
            if (type === 'Class' || type === 'Interface' || type === 'Enum') {
                structuralNodesMap.set(node.Id, node);
                classMembersMap.set(node.Id, []);
            }
        });

        // 2. Second Pass: Index Members (Methods, Properties, Fields, Constructors)
        // We use HAS_METHOD, HAS_PROPERTY, HAS_FIELD, HAS_CONSTRUCTOR to map members to classes
        allRelationships.forEach(rel => {
            const relType = rel.RelationshipType;
            if (relType.startsWith('HAS_')) {
                const ownerId = rel.FromNodeId;
                const memberId = rel.ToNodeId;
                
                if (structuralNodesMap.has(ownerId)) {
                    memberToOwnerMap.set(memberId, ownerId);
                    
                    const memberNode = allNodes.find(n => n.Id === memberId);
                    if (memberNode) {
                        classMembersMap.get(ownerId).push(memberNode);
                    }
                }
            }
        });
    }

    // Find the class/interface ID that owns a member node (Method/Property/etc.)
    function getOwnerClassId(nodeId) {
        if (structuralNodesMap.has(nodeId)) {
            return nodeId; // It is already a structural node
        }
        
        // Lookup in relationship map
        if (memberToOwnerMap.has(nodeId)) {
            return memberToOwnerMap.get(nodeId);
        }

        // Fallback: String matching parser (e.g. "method:Namespace.Class.MethodName" -> "class:Namespace.Class")
        const colonIdx = nodeId.indexOf(':');
        if (colonIdx === -1) return null;
        
        const path = nodeId.substring(colonIdx + 1);
        if (path.includes('..ctor')) {
            const classPath = path.substring(0, path.indexOf('..ctor'));
            return `class:${classPath}`;
        }

        let parts = path.split('.');
        while (parts.length > 1) {
            parts.pop();
            const candidate = parts.join('.');
            const classCandidate = `class:${candidate}`;
            if (structuralNodesMap.has(classCandidate)) return classCandidate;
            const interfaceCandidate = `interface:${candidate}`;
            if (structuralNodesMap.has(interfaceCandidate)) return interfaceCandidate;
            const enumCandidate = `enum:${candidate}`;
            if (structuralNodesMap.has(enumCandidate)) return enumCandidate;
        }

        return null;
    }

    // Transform and render the elements in Cytoscape
    function renderGraph() {
        const cyElements = [];

        // 1. Generate unique Namespaces as compound nodes
        const namespaces = new Set();
        structuralNodesMap.forEach(node => {
            if (node.Namespace) {
                namespaces.add(node.Namespace);
            }
        });

        // Add compound namespace nodes
        namespaces.forEach(ns => {
            cyElements.push({
                data: {
                    id: `ns:${ns}`,
                    label: ns,
                    type: 'Namespace'
                }
            });
        });

        // 2. Add structural nodes (Classes, Interfaces, Enums)
        structuralNodesMap.forEach(node => {
            const parentNs = node.Namespace ? `ns:${node.Namespace}` : undefined;
            cyElements.push({
                data: {
                    id: node.Id,
                    label: node.Name,
                    type: node.Type,
                    parent: parentNs,
                    namespace: node.Namespace
                }
            });
        });

        // 3. Map relationships to structural edges
        const edgeKeys = new Set(); // Prevent duplicate edges between the same classes
        allRelationships.forEach(rel => {
            const type = rel.RelationshipType;
            if (type.startsWith('HAS_') || type === 'BELONGS_TO_NAMESPACE' || type === 'BELONGS_TO_PROJECT') {
                return; // Internal structural containment relationships, skipped in visual graph
            }

            // Resolve owners (Methods/Properties are rolled up to their declaring Class)
            const fromClassId = getOwnerClassId(rel.FromNodeId);
            const toClassId = getOwnerClassId(rel.ToNodeId);

            if (fromClassId && toClassId && fromClassId !== toClassId) {
                const edgeKey = `${fromClassId}-${toClassId}-${type}`;
                
                if (!edgeKeys.has(edgeKey)) {
                    edgeKeys.add(edgeKey);
                    cyElements.push({
                        data: {
                            id: `edge:${fromClassId}-${toClassId}-${type}`,
                            source: fromClassId,
                            target: toClassId,
                            type: type
                        }
                    });
                }
            }
        });

        // Initialize Cytoscape Instance
        cyInstance = cytoscape({
            container: elements.container,
            elements: cyElements,
            style: getCytoscapeStylesheet(),
            layout: { name: 'null' } // Layout run immediately after in function
        });

        // Run layout
        runLayout();

        // Node Selection Hook
        cyInstance.on('tap', 'node', function(evt) {
            const node = evt.target;
            if (node.data('type') === 'Namespace') {
                resetSelectionHighlight();
                return; // Namespaces don't have separate details
            }
            highlightNodeDependencies(node);
            showNodeDetails(node.id());
        });

        // Background Tap Hook (deselect)
        cyInstance.on('tap', function(evt) {
            if (evt.target === cyInstance) {
                resetSelectionHighlight();
                hideNodeDetails();
            }
        });

        // Apply filters
        applyFilters();
    }

    // Run CoSE layout engine
    function runLayout() {
        if (!cyInstance) return;

        const layout = cyInstance.layout({
            name: 'cose',
            animate: true,
            animationDuration: 600,
            fit: true,
            padding: 40,
            nodeOverlap: 20,
            componentSpacing: 60,
            nodeRepulsion: function(node) { return 800000; },
            edgeElasticity: function(edge) { return 100; },
            nestingFactor: 1.2,
            gravity: 85,
            numIter: 1000,
            initialTemp: 200,
            coolingFactor: 0.95,
            minTemp: 1.0
        });

        layout.run();
    }

    // Cytoscape.js Stylesheet
    function getCytoscapeStylesheet() {
        return [
            {
                selector: 'node',
                style: {
                    'label': 'data(label)',
                    'color': '#f8fafc',
                    'font-family': 'Outfit, sans-serif',
                    'font-size': '11px',
                    'text-valign': 'center',
                    'text-halign': 'center',
                    'background-color': '#1e293b',
                    'border-width': '2px',
                    'border-color': '#475569',
                    'width': '65px',
                    'height': '65px',
                    'shape': 'ellipse',
                    'text-wrap': 'wrap',
                    'text-max-width': '60px',
                    'overlay-opacity': 0,
                    'transition-property': 'background-color, border-color, width, height, opacity',
                    'transition-duration': '0.2s'
                }
            },
            {
                selector: 'node[type="Class"]',
                style: {
                    'background-color': '#1e293b',
                    'border-color': typeColors['Class'],
                    'width': '70px',
                    'height': '70px',
                    'font-weight': '600'
                }
            },
            {
                selector: 'node[type="Interface"]',
                style: {
                    'background-color': '#2e2010',
                    'border-color': typeColors['Interface'],
                    'shape': 'round-rectangle',
                    'width': '65px',
                    'height': '65px'
                }
            },
            {
                selector: 'node[type="Enum"]',
                style: {
                    'background-color': '#0d251d',
                    'border-color': typeColors['Enum'],
                    'shape': 'hexagon',
                    'width': '55px',
                    'height': '55px'
                }
            },
            {
                selector: 'node[type="Namespace"]',
                style: {
                    'label': 'data(label)',
                    'text-valign': 'top',
                    'text-halign': 'center',
                    'background-color': 'rgba(15, 23, 42, 0.4)',
                    'border-width': '1.5px',
                    'border-color': 'rgba(99, 102, 241, 0.3)', // Violet border
                    'border-style': 'dashed',
                    'shape': 'round-rectangle',
                    'color': '#94a3b8',
                    'font-weight': 'bold',
                    'font-size': '12px',
                    'padding': '18px'
                }
            },
            {
                selector: 'edge',
                style: {
                    'width': 1.5,
                    'line-color': 'rgba(148, 163, 184, 0.25)',
                    'target-arrow-color': 'rgba(148, 163, 184, 0.25)',
                    'target-arrow-shape': 'triangle',
                    'curve-style': 'bezier',
                    'arrow-scale': 0.85,
                    'overlay-opacity': 0,
                    'transition-property': 'line-color, target-arrow-color, width',
                    'transition-duration': '0.2s'
                }
            },
            {
                selector: 'edge[type="USES"]',
                style: {
                    'line-color': 'rgba(59, 130, 246, 0.4)', // cyan/blue USES
                    'target-arrow-color': 'rgba(59, 130, 246, 0.5)'
                }
            },
            {
                selector: 'edge[type="CALLS"]',
                style: {
                    'line-color': 'rgba(192, 132, 252, 0.4)', // purple CALLS
                    'target-arrow-color': 'rgba(192, 132, 252, 0.5)',
                    'line-style': 'dotted',
                    'width': 1.2
                }
            },
            {
                selector: 'edge[type="INHERITS"]',
                style: {
                    'line-color': 'rgba(239, 68, 68, 0.5)', // red inherits
                    'target-arrow-color': 'rgba(239, 68, 68, 0.6)',
                    'width': 2
                }
            },
            {
                selector: 'edge[type="IMPLEMENTS"]',
                style: {
                    'line-color': 'rgba(245, 158, 11, 0.5)', // amber implements
                    'target-arrow-color': 'rgba(245, 158, 11, 0.6)',
                    'line-style': 'dashed',
                    'width': 1.8
                }
            },
            // Highlight styles
            {
                selector: 'node.highlighted',
                style: {
                    'border-width': '4px',
                    'border-color': '#22d3ee', // Glowing cyan border
                    'background-color': '#0f172a',
                    'scale': 1.15,
                    'z-index': 100
                }
            },
            {
                selector: 'node.connected',
                style: {
                    'opacity': 1.0,
                    'z-index': 90
                }
            },
            {
                selector: 'node.dimmed',
                style: {
                    'opacity': 0.25,
                    'z-index': 10
                }
            },
            {
                selector: 'edge.highlighted',
                style: {
                    'line-color': '#22d3ee',
                    'target-arrow-color': '#22d3ee',
                    'width': 3,
                    'z-index': 80
                }
            },
            {
                selector: 'edge.dimmed',
                style: {
                    'opacity': 0.1,
                    'z-index': 10
                }
            }
        ];
    }

    // Apply active filter checkboxes
    function applyFilters() {
        if (!cyInstance) return;

        const activeFilters = {
            'USES': elements.filterUses.checked,
            'CALLS': elements.filterCalls.checked,
            'INHERITS': elements.filterInherits.checked,
            'IMPLEMENTS': elements.filterImplements.checked
        };

        cyInstance.batch(() => {
            cyInstance.edges().forEach(edge => {
                const edgeType = edge.data('type');
                if (activeFilters[edgeType] === false) {
                    edge.style('display', 'none');
                } else {
                    edge.style('display', 'element');
                }
            });
        });
    }

    // Handle typing in the search box
    function handleSearch() {
        if (!cyInstance) return;

        const term = elements.search.value.toLowerCase().trim();
        if (term === '') {
            resetSelectionHighlight();
            return;
        }

        // Search in visible nodes
        const matches = cyInstance.nodes().filter(node => {
            if (node.data('type') === 'Namespace') return false;
            return node.data('label').toLowerCase().includes(term) || 
                   node.data('namespace').toLowerCase().includes(term);
        });

        cyInstance.batch(() => {
            if (matches.length > 0) {
                // Dim all
                cyInstance.elements().addClass('dimmed').removeClass('highlighted');
                
                // Highlight matches
                matches.addClass('highlighted').removeClass('dimmed');
                
                // Zoom & Center on first match
                cyInstance.animate({
                    center: matches[0],
                    zoom: 1.3,
                    duration: 500
                });
            } else {
                cyInstance.elements().removeClass('dimmed').removeClass('highlighted');
            }
        });
    }

    // Highlight a node and its direct connections, dimming everything else
    function highlightNodeDependencies(selectedNode) {
        if (!cyInstance) return;

        const connectedEdges = selectedNode.connectedEdges(':visible');
        const connectedNodes = connectedEdges.connectedNodes();

        cyInstance.batch(() => {
            // Dim everything
            cyInstance.elements().addClass('dimmed').removeClass('highlighted').removeClass('connected');
            
            // Highlight selected
            selectedNode.addClass('highlighted').removeClass('dimmed');
            
            // Highlight connected edges and nodes
            connectedEdges.addClass('highlighted').removeClass('dimmed');
            connectedNodes.addClass('connected').removeClass('dimmed');
            
            // Highlight parent compounds (namespaces) so they don't get completely blacked out
            cyInstance.nodes('[type="Namespace"]').removeClass('dimmed');
        });
    }

    // Reset graph selection highlighting
    function resetSelectionHighlight() {
        if (!cyInstance) return;

        cyInstance.batch(() => {
            cyInstance.elements().removeClass('dimmed').removeClass('highlighted').removeClass('connected');
        });
    }

    // Display metadata, properties, and relationships in the sidebar
    function showNodeDetails(nodeId) {
        const node = structuralNodesMap.get(nodeId);
        if (!node) return;

        elements.detailsEmpty.classList.add('hidden');
        elements.detailsContent.classList.remove('hidden');

        // Text & Badge styling
        elements.detailType.textContent = node.Type;
        elements.detailType.style.backgroundColor = typeColors[node.Type] + '25';
        elements.detailType.style.color = typeColors[node.Type];
        elements.detailType.style.borderColor = typeColors[node.Type] + '40';

        elements.detailName.textContent = node.Name;
        elements.detailNs.textContent = node.Namespace || 'Global Namespace';

        // Display Metadata badges (abstract, static, sealed, etc.)
        let propsHtml = '';
        if (node.Metadata) {
            for (const [key, value] of Object.entries(node.Metadata)) {
                if (value === 'True') {
                    const cleanKey = key.replace('Is', '');
                    propsHtml += `<span class="detail-prop-badge">${cleanKey}</span>`;
                }
            }
        }

        // Add class methods/fields as badges
        const members = classMembersMap.get(nodeId) || [];
        const methodCount = members.filter(m => m.Type === 'Method').length;
        const propCount = members.filter(m => m.Type === 'Property').length;
        const fieldCount = members.filter(m => m.Type === 'Field').length;

        if (methodCount > 0) propsHtml += `<span class="detail-prop-badge" style="color:var(--accent-emerald)">${methodCount} Methods</span>`;
        if (propCount > 0) propsHtml += `<span class="detail-prop-badge" style="color:var(--accent-cyan)">${propCount} Properties</span>`;
        if (fieldCount > 0) propsHtml += `<span class="detail-prop-badge" style="color:var(--accent-amber)">${fieldCount} Fields</span>`;

        elements.detailProps.innerHTML = propsHtml || '<span class="detail-prop-badge" style="color:var(--text-muted)">No modifiers</span>';

        // 3. Render Outgoing (Dependencies) & Incoming (Callers)
        const outgoingList = [];
        const incomingList = [];

        allRelationships.forEach(rel => {
            const relType = rel.RelationshipType;
            if (relType.startsWith('HAS_') || relType === 'BELONGS_TO_NAMESPACE' || relType === 'BELONGS_TO_PROJECT') {
                return; // Containment relationships skipped in list
            }

            const fromOwner = getOwnerClassId(rel.FromNodeId);
            const toOwner = getOwnerClassId(rel.ToNodeId);

            if (fromOwner === nodeId && toOwner && toOwner !== nodeId) {
                // Outgoing
                const targetNode = structuralNodesMap.get(toOwner);
                if (targetNode) {
                    outgoingList.push({ id: toOwner, name: targetNode.Name, type: relType });
                }
            }
            if (toOwner === nodeId && fromOwner && fromOwner !== nodeId) {
                // Incoming
                const sourceNode = structuralNodesMap.get(fromOwner);
                if (sourceNode) {
                    incomingList.push({ id: fromOwner, name: sourceNode.Name, type: relType });
                }
            }
        });

        // Render outgoing list
        if (outgoingList.length > 0) {
            // Deduplicate relations
            const uniqueOutgoing = deduplicateRelations(outgoingList);
            elements.detailOutgoingList.innerHTML = uniqueOutgoing.map(item => `
                <li onclick="window.CodeGraphVisualizer.selectNode('${item.id}')">
                    <span class="rel-target-name">${item.name}</span>
                    <span class="rel-type-badge">${item.type}</span>
                </li>
            `).join('');
        } else {
            elements.detailOutgoingList.innerHTML = '<li style="color:var(--text-muted); cursor:default; background:transparent; border:none; padding-left:0;">No outgoing dependencies</li>';
        }

        // Render incoming list
        if (incomingList.length > 0) {
            // Deduplicate relations
            const uniqueIncoming = deduplicateRelations(incomingList);
            elements.detailIncomingList.innerHTML = uniqueIncoming.map(item => `
                <li onclick="window.CodeGraphVisualizer.selectNode('${item.id}')">
                    <span class="rel-target-name">${item.name}</span>
                    <span class="rel-type-badge">${item.type}</span>
                </li>
            `).join('');
        } else {
            elements.detailIncomingList.innerHTML = '<li style="color:var(--text-muted); cursor:default; background:transparent; border:none; padding-left:0;">No incoming references</li>';
        }
    }

    // Helper: Deduplicate lists of dependencies
    function deduplicateRelations(list) {
        const seen = new Set();
        return list.filter(item => {
            const key = `${item.id}-${item.type}`;
            if (seen.has(key)) return false;
            seen.add(key);
            return true;
        }).sort((a,b) => a.name.localeCompare(b.name));
    }

    // Hide sidebar details panel
    function hideNodeDetails() {
        elements.detailsEmpty.classList.remove('hidden');
        elements.detailsContent.classList.add('hidden');
    }

    // External Interface: Select a node programmatically (e.g. from detail lists click)
    function selectNode(nodeId) {
        if (!cyInstance) return;

        const cyNode = cyInstance.getElementById(nodeId);
        if (cyNode.length > 0) {
            cyInstance.animate({
                center: cyNode,
                zoom: 1.3,
                duration: 400
            });
            highlightNodeDependencies(cyNode);
            showNodeDetails(nodeId);
        }
    }

    // Expose methods to global window namespace
    window.CodeGraphVisualizer = {
        init: init,
        refresh: loadGraphData,
        selectNode: selectNode
    };
})();
