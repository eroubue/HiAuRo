import { defineComponent, h } from 'vue';

export const ShapesSymbolArrowLeft3 = defineComponent({
  name: 'ShapesSymbolArrowLeft3',
  props: {
    class: {
      type: String,
      default: ''
    }
  },
  setup(props, { attrs }) {
    return () => h(
      'svg',
      {
        viewBox: '0 0 20 20',
        
        class: `game-icons ${props.class}`,
        ...attrs
      },
      [
        h('path', {"d": "M6.38644 2.29964C6.71789 2.10083 7.14845 2.20851 7.34738 2.53987C7.54627 2.87135 7.43859 3.30188 7.10715 3.50081L5.69385 4.34824C5.2363 4.62259 5.00753 4.75976 4.98692 4.95874C4.98409 4.9861 4.98408 5.01368 4.98691 5.04103C5.00746 5.24002 5.23619 5.37726 5.69366 5.65174L7.10715 6.49983C7.43854 6.69873 7.54615 7.12931 7.34738 7.46077C7.14844 7.79216 6.7179 7.8989 6.38644 7.70003L2.89426 5.60432C2.43799 5.33047 2.4379 4.66915 2.89426 4.39534L6.38644 2.29964Z", "fillRule": "evenodd"})
      ]
    );
  }
});
