import { defineComponent, h } from 'vue';

export const ShapesSymbolEquals = defineComponent({
  name: 'ShapesSymbolEquals',
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
        h('path', {"d": "M7.09961 5.5498C7.4862 5.5498 7.79979 5.86341 7.7998 6.25C7.7998 6.6366 7.48621 6.9502 7.09961 6.9502H2.90039C2.51379 6.9502 2.2002 6.6366 2.2002 6.25C2.20021 5.86341 2.5138 5.5498 2.90039 5.5498H7.09961ZM7.09961 3.0498C7.48621 3.0498 7.7998 3.3634 7.7998 3.75C7.79973 4.13653 7.48616 4.4502 7.09961 4.4502H2.90039C2.51384 4.4502 2.20027 4.13653 2.2002 3.75C2.2002 3.3634 2.51379 3.0498 2.90039 3.0498H7.09961Z", "fillRule": "evenodd"})
      ]
    );
  }
});
